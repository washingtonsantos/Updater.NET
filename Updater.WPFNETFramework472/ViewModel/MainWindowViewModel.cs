using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Updater.WPFNETFramework472.Model;

namespace Updater.WPFNETFramework472.ViewModel
{
    public class MainWindowViewModel : BindableBase
    {
        private static string url = @"http://extranet.mgcontecnica.com.br:8000/download/RegistroPonto.zip";
        private static string caminhoTemporario = $@"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\Downloads\pontoVirtualTemp\download";
        private static string caminhoTemporarioBKP = $@"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\Downloads\pontoVirtualTemp\bkp";
        private static string _pathPadrao = $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\Programs\Registro Ponto";
        private static string _nameFileZipToDownload = "RegistroPonto.zip";

        public MainWindowViewModel()
        {
            Infos = new ObservableCollection<string>();

            Processar();
        }

        #region [COMMANDS]
        private DelegateCommand _downloadCommand;
        public DelegateCommand DownloadCommand =>
            _downloadCommand ?? (_downloadCommand = new DelegateCommand(Processar));
        #endregion

        #region [PROPERTIES]
        private string _percentage;
        public string Percentage
        {
            get { return _percentage; }
            set { SetProperty(ref _percentage, value); }
        }

        private int _currentProgress;
        public int CurrentProgress
        {
            get { return _currentProgress; }
            set { SetProperty(ref _currentProgress, value); }
        }

        private int _currentProgressInstall;
        public int CurrentProgressInstall
        {
            get { return _currentProgressInstall; }
            set { SetProperty(ref _currentProgressInstall, value); }
        }

        private ObservableCollection<string> _infos;
        public ObservableCollection<string> Infos 
        {
            get { return _infos; }
            set { SetProperty(ref _infos, value); }
        }
        #endregion

        #region [METHODS]
        private void CriarCaminhoTemp()
        {
            try
            {
                Infos.Add("Criando Caminho Temporário OK");

                if (!Directory.Exists(caminhoTemporario))
                {
                    Directory.CreateDirectory(caminhoTemporario);
                    Infos.Add("Diretório Temporário Criado");
                }

                if (!Directory.Exists(caminhoTemporarioBKP))
                {
                    Directory.CreateDirectory(caminhoTemporarioBKP);
                    Infos.Add("Diretório Temporário BKP Criado");
                }
            }
            catch (Exception ex)
            {
                Infos.Add("Falha ao criar Diretório Temporário Criado");
                Infos.Add("Processo de Atualização Cancelado");
                Roolback();
            }
        }

        /// <summary>
        /// Inicia efetuando Download
        /// </summary>
        private async void Processar()
        {
            CriarCaminhoTemp();

            Infos.Add("Efetuando Download");
            //construct Progress<T>, passing ReportProgress as the Action<T> 
            var progressIndicator = new Progress<ProgressEventArgsEx>(ReportProgress);
            await DownloadStraingAsyncronous(url, progressIndicator);
        }

        public async Task<string> DownloadStraingAsyncronous(string url, IProgress<ProgressEventArgsEx> progress)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    var ur = new Uri(url);
                    // client.Credentials = new NetworkCredential("username", "password");
                    client.DownloadProgressChanged += client_DownloadProgressChanged;
                    client.DownloadFileCompleted += client_DownloadFileCompleted;
                    Console.WriteLine(@"Downloading file:");
                    client.DownloadFileAsync(ur, caminhoTemporario + "\\RegistroPonto.zip");
                    return File.Exists(caminhoTemporario).ToString();
                }
            }
            catch (Exception ex)
            {
                Infos.Add("Falha no Download, Processo de Atualização Cancelado");
                Roolback();
            }

            Infos.Add("Download concluído");

            return string.Empty;
        }

        void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {           
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;
            Percentage = String.Format("{0:0.##}", percentage.ToString()) + "%";

            CurrentProgress = int.Parse(Math.Truncate(percentage).ToString());
        }

        void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                CurrentProgressInstall = 20;
                
                bool bkpOK = BackupLastVersion();

                if (!bkpOK) Roolback();

                Infos.Add("Instalando");

                CurrentProgressInstall = 40;
                bool descompactou = Descompacta();

                if (!descompactou) Roolback();

                CurrentProgressInstall = 60;

                RemoveDownloadZIP();
                RemoveLastVersion();

                CurrentProgressInstall = 80;

                bool filesCopy = CopyFilesToDirectoryDefault();

                if (!filesCopy)
                {
                    RetornaVersaoAnterior();
                    Roolback();
                }
                RemoveBackLastVersion();
                RemoveDownload();

                CurrentProgressInstall = 100;

                Infos.Add("Atualizado com sucesso!");

                var executalvelStart = _pathPadrao + "\\PontoVirtual.Presentation.Wpf.exe";
                Process.Start(executalvelStart);

                App.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Infos.Add("Falha na instalação, Atualização Cancelada");
            }
        }

        private void ReportProgress(ProgressEventArgsEx args)
        {
            Percentage = args.Text + " " + args.Percentage;
        }

        private bool RemoveLastVersion()
        {
            try
            {
                Infos.Add("Removendo versão anterior...");

                var dir = new DirectoryInfo(_pathPadrao);

                // Get the files in the directory and copy them to the new location.
                FileInfo[] files = dir.GetFiles();
                int countFiles = files.Length;

                foreach (FileInfo file in files)
                {
                    file.Delete();
                }
            }
            catch (Exception ex)
            {
                Infos.Add("Falha na Instalação, Atualização Cancelada");
                return false;
            }

            return true;
        }

        private bool CopyFilesToDirectoryDefault()
        {
            try
            {
                // Get the files in the directory and copy them to the new location.
                FileInfo[] files = new DirectoryInfo(caminhoTemporario).GetFiles();
                int countFiles = files.Length;

                foreach (FileInfo file in files)
                {
                    string _pathDefault = Path.Combine(_pathPadrao, file.Name);
                    file.CopyTo(_pathDefault, false);
                }
            }
            catch (Exception)
            {
                Infos.Add("Falha na Instação, Atualização Cancelada");
            }

            return true;
        }
       
        private bool RetornaVersaoAnterior()
        {
            try
            {
                // Get the files in the directory and copy them to the new location.
                FileInfo[] files = new DirectoryInfo(caminhoTemporarioBKP).GetFiles();
                int countFiles = files.Length;

                foreach (FileInfo file in files)
                {
                    string _pathDefault = Path.Combine(_pathPadrao, file.Name);
                    file.CopyTo(_pathDefault, false);
                }
            }
            catch (Exception)
            {
                Infos.Add("Falha na restauração da versão anterior, Atualização Cancelada");
            }

            return true;
        }

        private bool BackupLastVersion()
        {
            try
            {
                Infos.Add("Iniciando BKP versão anterior");

                var dir = new DirectoryInfo(_pathPadrao);

                // Get the files in the directory and copy them to the new location.
                FileInfo[] files = dir.GetFiles();
                int countFiles = files.Length;

                foreach (FileInfo file in files)
                {
                    string tempPath = Path.Combine(caminhoTemporarioBKP, file.Name);
                    file.CopyTo(tempPath, false);
                }
               
            }
            catch (Exception ex)
            {
                Infos.Add("Falha no BKP versão anterior, Atualização Cancelada");
                return false;
            }

            Infos.Add("BKP versão anterior concluído");

            return true;
        }
        
        private void RemoveBackLastVersion()
        {
            try
            {
                var dir = new DirectoryInfo(caminhoTemporarioBKP);

                // Get the files in the directory and copy them to the new location.
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files)
                {
                    file.Delete();
                }

                if (Directory.Exists(caminhoTemporarioBKP))
                    Directory.Delete(caminhoTemporarioBKP);
            }
            catch (Exception)
            {
                Infos.Add("Falha na  remoção do BKP da versão anterior, Atualização Cancelada");
            }

            Infos.Add("BKP removido...");
        }

        private void RemoveDownloadZIP()
        {
            try
            {
                File.Exists(caminhoTemporario +"\\"+ _nameFileZipToDownload);
                  File.Delete(caminhoTemporario + "\\" +  _nameFileZipToDownload);
            }
            catch (Exception)
            {
                Infos.Add("Falha no BKP versão anterior, Atualização Cancelada");
            }

            Infos.Add("Download removido...");
        }

        private void RemoveDownload()
        {
            try
            {
                var dir = new DirectoryInfo(caminhoTemporario);

                // Get the files in the directory and copy them to the new location.
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files)
                {
                    file.Delete();
                }

                if (Directory.Exists(caminhoTemporario))
                    Directory.Delete(caminhoTemporario);
            }
            catch (Exception)
            {
                Infos.Add("Falha na remoção do download, Atualização Cancelada");
            }

            Infos.Add("Download removido...");
        }

        private bool Descompacta()
        {
            try
            {
                var gzipFileName = new DirectoryInfo(caminhoTemporario).GetFiles("RegistroPonto.zip", SearchOption.TopDirectoryOnly).FirstOrDefault();

                string decompressedFileName = $@"{caminhoTemporario}\{gzipFileName.Name}";

                using (ZipArchive archive = ZipFile.OpenRead(decompressedFileName))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                            // Gets the full path to ensure that relative segments are removed.
                            string destinationPath = Path.GetFullPath(Path.Combine(caminhoTemporario, entry.FullName));

                            // Ordinal match is safest, case-sensitive volumes can be mounted within volumes that
                            // are case-insensitive.
                            if (destinationPath.StartsWith(caminhoTemporario, StringComparison.Ordinal))
                                entry.ExtractToFile(destinationPath);
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Infos.Add("Falha ao Descompactar, Atualização Cancelada");
                return false;
            }
        }

        private bool Roolback()
        {
            CurrentProgressInstall = 80;
            RemoveBackLastVersion();
            CurrentProgressInstall = 50;
            RemoveDownload();
            CurrentProgressInstall = 30;

            return true;
        }
        #endregion
    }
}
