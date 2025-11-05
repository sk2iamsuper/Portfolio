using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using tcp_socket.Services;
using tcp_socket.Models;

var files = Directory.GetFiles(folderBackup, "*.*");

foreach (var file in files)
  {
  var name = Path.GetFileName(file);
  // If file represents CMWIPINF, parse and update DB
  if (name.Contains("CMWIPINF"))
  {
  try
  {
    using var sr = new StreamReader(file, Encoding.ASCII);
    string line;
    while ((line = sr.ReadLine()) != null)
    {
      if (line.Length >= 1000)
    {
  var model = _parser.Parse(line);
  // Lazy init DB service (please set connection string below)
    if (_dbService == null)
    {
    // TODO: set real connection string here or via config
      _dbService = new DbService("server=127.0.0.1;uid=root;pwd=;database=ufd;Charset=utf8;");
    }
  _dbService.InsertWipInfo(model);
  }
}

// after processing, move file to dated backup folder
var datedBackup = Path.Combine(_folders[3], DateTime.Now.ToString("yyyyMMdd"));
_fileService.EnsureFolders(datedBackup);
_fileService.MoveToUnique(file, datedBackup);

// create CMWIPINF file for OMS if needed (omitted here — implement if required)
}
catch (Exception ex)
{
  txt_TcpContents.AppendText($"\r\nProcessHmvBackup error: {ex.Message}");
  }
  }
  else
  {
  // Non-CMWIPINF files: just archive
  var datedBackup = Path.Combine(_folders[3], DateTime.Now.ToString("yyyyMMdd"));
  _fileService.EnsureFolders(datedBackup);
  _fileService.MoveToUnique(file, datedBackup);
  }
  }
}

// Clean up on form closing
private void Form1_FormClosing(object sender, FormClosingEventArgs e)
{
  timer1_1?.Stop();
  _dbService?.Dispose();
  DisposeSocket();
  }
}
}
