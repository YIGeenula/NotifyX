using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using CodexBlaze.PaymentReminder.Core.Data;
using Serilog;

namespace CodexBlaze.PaymentReminder.Core.Services;

public interface IBackupRestoreService
{
    Task<string> CreateBackupAsync(string targetDirectory);
    Task<bool> RestoreBackupAsync(string backupZipPath);
    List<string> GetBackupHistory(string backupDirectory);
}

public class BackupRestoreService : IBackupRestoreService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly string _dbFilePath;

    public BackupRestoreService(IDbContextFactory<AppDbContext> contextFactory, string dbFilePath)
    {
        _contextFactory = contextFactory;
        _dbFilePath = dbFilePath;
    }

    public async Task<string> CreateBackupAsync(string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupFileName = $"CodexBlaze_Backup_{timestamp}.cbz";
        string backupFilePath = Path.Combine(targetDirectory, backupFileName);

        // Perform SQLite VACUUM INTO to create a safe, uncorrupted hot copy of SQLite
        string tempDbCopy = Path.Combine(Path.GetTempPath(), $"cb_backup_{Guid.NewGuid()}.db");
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlRawAsync($"VACUUM INTO '{tempDbCopy}'");
        }

        try
        {
            using (var zip = ZipFile.Open(backupFilePath, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(tempDbCopy, "database.db");
                
                // Add attachments directory if exists
                string attachmentsDir = Path.Combine(Path.GetDirectoryName(_dbFilePath) ?? "", "attachments");
                if (Directory.Exists(attachmentsDir))
                {
                    foreach (var file in Directory.GetFiles(attachmentsDir, "*.*", SearchOption.AllDirectories))
                    {
                        string relativePath = Path.GetRelativePath(attachmentsDir, file);
                        zip.CreateEntryFromFile(file, Path.Combine("attachments", relativePath));
                    }
                }
            }

            Log.Information("Backup created successfully at {Path}", backupFilePath);
            return backupFilePath;
        }
        finally
        {
            if (File.Exists(tempDbCopy))
            {
                File.Delete(tempDbCopy);
            }
        }
    }

    public async Task<bool> RestoreBackupAsync(string backupZipPath)
    {
        if (!File.Exists(backupZipPath))
        {
            throw new FileNotFoundException("Backup file not found.", backupZipPath);
        }

        string tempExtract = Path.Combine(Path.GetTempPath(), $"cb_restore_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempExtract);

        try
        {
            ZipFile.ExtractToDirectory(backupZipPath, tempExtract);
            string extractedDb = Path.Combine(tempExtract, "database.db");

            if (!File.Exists(extractedDb))
            {
                throw new InvalidOperationException("Invalid backup archive: database.db missing.");
            }

            // Create a safety backup of current live database first
            string appDataDir = Path.GetDirectoryName(_dbFilePath)!;
            string safetyCopy = Path.Combine(appDataDir, $"pre_restore_safety_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            if (File.Exists(_dbFilePath))
            {
                File.Copy(_dbFilePath, safetyCopy, true);
            }

            // Overwrite database file
            File.Copy(extractedDb, _dbFilePath, true);

            // Restore attachments if any
            string extractedAttachments = Path.Combine(tempExtract, "attachments");
            if (Directory.Exists(extractedAttachments))
            {
                string targetAttachments = Path.Combine(appDataDir, "attachments");
                Directory.CreateDirectory(targetAttachments);
                foreach (var file in Directory.GetFiles(extractedAttachments, "*.*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(extractedAttachments, file);
                    string dest = Path.Combine(targetAttachments, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest, true);
                }
            }

            Log.Information("Database restored successfully from {BackupPath}", backupZipPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to restore backup from {BackupPath}", backupZipPath);
            throw;
        }
        finally
        {
            if (Directory.Exists(tempExtract))
            {
                Directory.Delete(tempExtract, true);
            }
        }
    }

    public List<string> GetBackupHistory(string backupDirectory)
    {
        if (!Directory.Exists(backupDirectory)) return new List<string>();
        return Directory.GetFiles(backupDirectory, "*.cbz")
            .OrderByDescending(File.GetCreationTime)
            .ToList();
    }
}
