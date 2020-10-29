using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using Updater.WPF.Model;

namespace Updater.WPF.ViewModel
{
    public class PrincipalWindowViewModel : BindableBase
    {
        private static string url = @"http://extranet.mgcontecnica.com.br:8000/download/RegistroPonto.zip";
        private static string caminhoTemporario = $@"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\Downloads\pontoVirtualTemp\download";
        private static string caminhoTemporarioBKP = $@"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\Downloads\pontoVirtualTemp\bkp";
        private static string _pathPadrao = $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\Programs\Registro Ponto";
        private static string _nameFileZipToDownload = "RegistroPonto.zip";

        public PrincipalWindowViewModel()
        {
            PanelInstalacaoVisibility = Visibility.Collapsed;

            Infos = new ObservableCollection<string>();

            IniciarDownload();
        }

        #region [COMMANDS]
        private DelegateCommand _downloadCommand;
        public DelegateCommand DownloadCommand =>
            _downloadCommand ?? (_downloadCommand = new DelegateCommand(IniciarDownload));
        #endregion

        #region [PROPERTIES]
        private Visibility _panelDownloadVisibility;
        public Visibility PanelDownloadVisibility
        {
            get { return _panelDownloadVisibility; }
            set { SetProperty(ref _panelDownloadVisibility, value); }
        }

        private Visibility _panelInstalacaoVisibility;
        public Visibility PanelInstalacaoVisibility
        {
            get { return _panelInstalacaoVisibility; }
            set { SetProperty(ref _panelInstalacaoVisibility, value); }
        }

        private string _percentage;
        public string PercentageDownload
        {
            get { return _percentage; }
            set { SetProperty(ref _percentage, value); }
        }

        private string _percentageInstall;
        public string PercentageInstall
        {
            get { return _percentageInstall; }
            set { SetProperty(ref _percentageInstall, value); }
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

        private string _infoInstalacao;
        public string InfoInstalacao
        {
            get { return _infoInstalacao; }
            set { SetProperty(ref _infoInstalacao, value); }
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
                if (!Directory.Exists(caminhoTemporario))
                    Directory.CreateDirectory(caminhoTemporario);

                if (!Directory.Exists(caminhoTemporarioBKP))
                    Directory.CreateDirectory(caminhoTemporarioBKP);
            }
            catch (Exception ex)
            {
                Infos.Add("Falha ao criar Diretório Temporário Criado");
                Infos.Add("Processo de Atualização Cancelado");
                Roolback();
            }

            Infos.Add("Criando Caminho Temporário OK");
        }

        /// <summary>
        /// Inicia efetuando Download
        /// </summary>
        private async void IniciarDownload()
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
            PercentageDownload = percentage.ToString("0.##");

            CurrentProgress = int.Parse(Math.Truncate(percentage).ToString());
        }

        void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            Processar();
        }

        private async void Processar()
        {
            try
            {
                var processInstall = Task.Factory.StartNew(() => 
                {
                    PanelInstalacaoVisibility = Visibility.Visible;
                    PanelDownloadVisibility = Visibility.Collapsed;

                    ReportProgressInstall(1);
                    InfoInstalacao = "Efetuando bkp versão anterior";
                    bool bkpOK = BackupLastVersion();

                    if (!bkpOK) Roolback();

                    ReportProgressInstall(2);
                    InfoInstalacao = "Descompactando arquivos";
                    bool descompactou = Descompacta();

                    if (!descompactou) Roolback();

                    ReportProgressInstall(3);
                    InfoInstalacao = "Removendo arquivos temporários";
                    RemoveDownloadZIP();
                    ReportProgressInstall(4);
                    RemoveLastVersion();

                    ReportProgressInstall(6);
                    CurrentProgressInstall = 80;
                    InfoInstalacao = "finalizando atualização";
                    bool filesCopy = CopyFilesToDirectoryDefault();

                    if (!filesCopy)
                    {
                        RetornaVersaoAnterior();
                        Roolback();
                    }

                    ReportProgressInstall(8);
                    InfoInstalacao = "Removendo arquivos temporários";
                    RemoveBackLastVersion();
                    ReportProgressInstall(9);
                    RemoveDownload();

                    ReportProgressInstall(10);
                    InfoInstalacao = "Atualizado com sucesso!";
                    Infos.Add("Atualizado com sucesso!");

                    System.Threading.Thread.Sleep(2000);

                    var executalvelStart = _pathPadrao + "\\PontoVirtual.Presentation.Wpf.exe";
                    Process.Start(executalvelStart);

                });

                await processInstall;

                App.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Infos.Add("Falha na instalação, Atualização Cancelada");
                App.Current.Shutdown();
            }
        }

        private void ReportProgress(ProgressEventArgsEx args)
        {
            PercentageDownload = args.Text + " " + args.Percentage;
        }

        private void ReportProgressInstall(int methodExecutado)
        {
            PercentageInstall = (100 * methodExecutado / 10).ToString("0.##");
            CurrentProgressInstall = 20 * methodExecutado;
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
                File.Exists(caminhoTemporario + "\\" + _nameFileZipToDownload);
                File.Delete(caminhoTemporario + "\\" + _nameFileZipToDownload);
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
