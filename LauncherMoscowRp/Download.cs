using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace LauncherMoscowRp
{
    class FileInfoEntry
    {
        public string FileName { get; }
        public string ExpectedMD5 { get; }

        public FileInfoEntry(string fileName, string expectedMD5)
        {
            FileName = fileName;
            ExpectedMD5 = expectedMD5;
        }
    }

    public class UpdateFilesArgs : EventArgs
    {
        public float speed = 0;
        public decimal percents = 0;
        public string downloadfile = "";
        public int filestatus = 0;
        public long filesize = 0;
    }

    internal class Download
    {
        private CancellationTokenSource cancellationTokenSource;
        public event EventHandler<UpdateFilesArgs> onUpdateDownload;
        public event EventHandler onUpdateCompleted;
        private int filesToDownload;
        public int IsUpdate = 0;

        public async Task Update()
        {
            cancellationTokenSource = new CancellationTokenSource();
            string basePath = Settings.GamePath; // Путь к локальным файлам
            string fileInfoUrl = "ваш файл"; // Замените на реальный URL

            try
            {
                List<FileInfoEntry> fileInfoEntries = await ReadFileInfo(fileInfoUrl, cancellationTokenSource.Token);


                // Проверка и обновление файлов
                IsUpdate = 1;
                await CheckAndUpdateFiles(basePath, fileInfoEntries, cancellationTokenSource.Token);

                onUpdateCompleted(this, EventArgs.Empty);
                Console.WriteLine("Проверка и обновление завершены.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");

                if (ex is HttpRequestException httpRequestException && httpRequestException.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {httpRequestException.InnerException.Message}");
                }
            }
        }

        static async Task<List<FileInfoEntry>> ReadFileInfo(string fileInfoUrl, CancellationToken cancellationToken)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(fileInfoUrl, cancellationToken);
                Console.WriteLine(response);

                if (response.IsSuccessStatusCode)
                {
                    string fileInfoContent = await response.Content.ReadAsStringAsync();
                    return ParseFileInfo(fileInfoContent);
                }
                else
                {
                    throw new Exception($"Ошибка при получении файла информации: {response.StatusCode}");
                }
            }
        }

        static List<FileInfoEntry> ParseFileInfo(string fileInfoContent)
        {
            List<FileInfoEntry> fileInfoEntries = new List<FileInfoEntry>();

            using (StringReader reader = new StringReader(fileInfoContent))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(',');

                    if (parts.Length == 2)
                    {
                        string fileName = parts[0].Trim();
                        string expectedMD5 = parts[1].Trim();

                        fileInfoEntries.Add(new FileInfoEntry(fileName, expectedMD5));
                    }
                }
            }

            return fileInfoEntries;
        }

        private async Task CheckAndUpdateFiles(string basePath, List<FileInfoEntry> fileInfoEntries, CancellationToken cancellationToken)
        {
            UpdateFilesArgs updateFilesArgs = new UpdateFilesArgs();
            filesToDownload = fileInfoEntries.Count;

            foreach (var entry in fileInfoEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string filePath = Path.Combine(basePath, entry.FileName);

                if (File.Exists(filePath))
                {
                    string actualMD5 = CalculateMD5(filePath);

                    if (!actualMD5.Equals(entry.ExpectedMD5, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Хэши не совпадают для файла {entry.FileName}. Обновление...");

                        // Скачивание файла с сервера и обновление локального файла
                        await DownloadAndUpdateFile(entry.FileName, basePath, cancellationToken);
                    }
                    else
                    {
                        Console.WriteLine($"Хэши совпадают для файла {entry.FileName}.");
                        filesToDownload--;
                        CheckDownloadCompletion();
                    }
                }
                else
                {
                    Console.WriteLine($"Локальный файл {entry.FileName} отсутствует. Скачивание...");

                    // Скачивание файла с сервера
                    await DownloadAndUpdateFile(entry.FileName, basePath, cancellationToken);
                }
            }
        }

        private async Task DownloadAndUpdateFile(string fileName, string basePath, CancellationToken cancellationToken)
        {
            string fileUrl = $"/{fileName}"; // Замените URL

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Создание директории, если она не существует
                    string filePath = Path.Combine(basePath, fileName);
                    string directoryPath = Path.GetDirectoryName(filePath);

                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    long fileSize = await GetFileSizeAsync(fileUrl, client, cancellationToken);

                    UpdateFilesArgs updateFilesArgs = new UpdateFilesArgs();
                    updateFilesArgs.downloadfile = fileName;
                    updateFilesArgs.filestatus = 2;
                    updateFilesArgs.filesize = fileSize;
                    onUpdateDownload(this, updateFilesArgs);

                    // Скачивание файла
                    //byte[] fileBytes = await client.GetByteArrayAsync(fileUrl);
                    //cancellationToken.ThrowIfCancellationRequested();
                    //File.WriteAllBytes(Path.Combine(basePath, fileName), fileBytes);
                    using (HttpResponseMessage response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead)) // Используем HttpCompletionOption.ResponseHeadersRead для частичного скачивания
                    {
                        response.EnsureSuccessStatusCode(); // Гарантирует успешный ответ (код 200-299)

                        using (Stream contentStream = await response.Content.ReadAsStreamAsync(), fileStream = File.Create(Path.Combine(basePath, fileName)))
                        {
                            byte[] buffer = new byte[81920];
                            int bytesRead;
                            long totalBytesRead = 0;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                            {
                                fileStream.Write(buffer, 0, bytesRead);

                                totalBytesRead += bytesRead;

                                updateFilesArgs.percents = (decimal)totalBytesRead / fileSize * 100;
                                onUpdateDownload(this, updateFilesArgs);


                                if (cancellationToken.IsCancellationRequested)
                                {
                                    Console.WriteLine($"Скачивание файла {fileName} отменено.");
                                    return; // Важно вернуться, чтобы не продолжать выполнение после отмены
                                }
                            }
                        }
                    }

                    Console.WriteLine($"Файл {fileName} успешно скачан и обновлен.");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Скачивание файла {fileName} отменено.");
                    // Можно добавить дополнительную логику для отмены скачивания, если необходимо
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при скачивании файла {fileName}: {ex.Message}");
                }
            }
        }

        private async Task<long> GetFileSizeAsync(string fileUrl, HttpClient client, CancellationToken cancellationToken)
        {
            using (var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                return response.Content.Headers.ContentLength ?? 0;
            }
        }

        static string CalculateMD5(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLower();
                }
            }
        }

        //Checker
        private void CheckDownloadCompletion()
        {
            if (filesToDownload == 0)
            {
                IsUpdate = 2;
            }
        }

        public void CancelDownload()
        {
            cancellationTokenSource?.Cancel();
            IsUpdate = 0;
        }
    }

}