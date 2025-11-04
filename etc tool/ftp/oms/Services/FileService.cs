using System;
using System.IO;

namespace tcp_socket.Services
{
  public class FileService
  {
  public void EnsureFolders(params string[] folders)
  {
    foreach (var f in folders)
    {
      if (!Directory.Exists(f)) Directory.CreateDirectory(f);
    }
  }
  
  public string CopyToBackup(string sourceFile, string backupFolder)
  {
    var fi = new FileInfo(sourceFile);
    var dest = Path.Combine(backupFolder, fi.Name);
    var uniqueDest = GetUniqueFileName(dest);
    File.Copy(sourceFile, uniqueDest);
    return uniqueDest;
  }
  
  public string MoveToUnique(string sourceFile, string targetFolder)
  {
    var fi = new FileInfo(sourceFile);
    var dest = Path.Combine(targetFolder, fi.Name);
    var uniqueDest = GetUniqueFileName(dest);
    File.Move(sourceFile, uniqueDest);
    return uniqueDest;
  }
  
  private string GetUniqueFileName(string path)
  {
    var dir = Path.GetDirectoryName(path) ?? string.Empty;
    var name = Path.GetFileNameWithoutExtension(path);
    var ext = Path.GetExtension(path);
    var candidate = path;
    int i = 0;
    while (File.Exists(candidate))
    {
      candidate = Path.Combine(dir, $"{name}_{i}{ext}");
      i++;
    }
    return candidate;
  }
  
  public void SafeDelete(string path)
  {
    try { if (File.Exists(path)) File.Delete(path); } catch { }
  }
  }
}
