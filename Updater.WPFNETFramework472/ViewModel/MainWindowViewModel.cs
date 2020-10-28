using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Updater.WPFNETFramework472.Model;

namespace Updater.WPFNETFramework472.ViewModel
{
    public class MainWindowViewModel : BindableBase
    {
        private static string url = @"http://extranet.mgcontecnica.com.br:8000/download/RegistroPonto.exe";
        private static string caminhoTemporario = $@"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\Downloads\pontoVirtualTemp";
        private static string caminhoTemporarioBKP = $@"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\Downloads\pontoVirtualTemp\bkp";
        private static string _pathPadrao = $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\Programs\Registro Ponto";
        
        public MainWindowViewModel()
        {
            Infos = new ObservableCollection<Info>();
            CriarCaminhoTemp();
            ExecutarDownloadCommand();
        }

        #region [COMMANDS]
        private DelegateCommand _downloadCommand;
        public DelegateCommand DownloadCommand =>
            _downloadCommand ?? (_downloadCommand = new DelegateCommand(ExecutarDownloadCommand));
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
        private ObservableCollection<Info> _infos;
        public ObservableCollection<Info> Infos 
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
                InsertObservableInfos(1, "Criando Caminho Temporário OK", "information");

                if (!Directory.Exists(caminhoTemporario))
                {
                    Directory.CreateDirectory(caminhoTemporario);
                    UpdateObservableInfos(10, "Diretório Caminho Temporário Criado");
                }
            }
            catch (Exception ex)
            {
                UpdateObservableInfos(1, "Falha ao criar Caminho Temporário");
                UpdateObservableInfos(10, "Processo de Atualização Cancelado");
            }

        }

        private async void ExecutarDownloadCommand()
        {
            Infos.Add(new Info { Id = 2, Mensagem = "Efetuando Download" });
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
                    client.DownloadFileAsync(ur, caminhoTemporario + "\\RegistroPonto.exe");
                    return File.Exists(caminhoTemporario).ToString();
                }
            }
            catch (Exception ex)
            {
                UpdateObservableInfos(10, "Falha no Download, Atualização Cancelada");
            }

            UpdateObservableInfos(2, "Download Concluído");

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
                bool bkpOK = BackupLastVersion();

                if (bkpOK)
                    StartInstalation();
                else
                    RemoveBackLastVersion();
            }
            catch (Exception ex)
            {
                UpdateObservableInfos(10, "Atualização Cancelada");
            }
        }

        private void ReportProgress(ProgressEventArgsEx args)
        {
            Percentage = args.Text + " " + args.Percentage;
        }

        private bool BackupLastVersion()
        {
            try
            {
                Infos.Add(new Info { Id = 3, Mensagem = "Iniciando BKP versão anterior"});

                if (!Directory.Exists(caminhoTemporarioBKP))
                    Directory.CreateDirectory(caminhoTemporarioBKP);

                var dir = new DirectoryInfo(_pathPadrao);

                // Get the files in the directory and copy them to the new location.
                FileInfo[] files = dir.GetFiles();
                int countFiles = files.Length;

                foreach (FileInfo file in files)
                {
                    UpdateObservableInfos(3, "Efetuando BKP versão anterior " + (1 / countFiles * 100) + "%");
                    string tempPath = Path.Combine(caminhoTemporarioBKP, file.Name);
                    file.CopyTo(tempPath, false);
                }
               
            }
            catch (Exception ex)
            {
                UpdateObservableInfos(3, "Falha no BKP da versão anterior, Atualização Cancelada");
                return false;
            }

            UpdateObservableInfos(3, "BKP versão anterior concluído");
            
            return true;
        }
        
        private void RemoveBackLastVersion()
        {
            var dir = new DirectoryInfo(caminhoTemporarioBKP);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                file.Delete();
            }
        }

        private bool StartInstalation()
        {
            InsertObservableInfos(4, "Iniciando Instalação", "information");

            //Extrair ZIP

            //Copiar Para a Pasta

            return true;
        }

        private void InsertObservableInfos(int id, string novaMensagem, string icon = null)
        {
            Infos.Add(new Info { Id = id, Mensagem = novaMensagem});
        }
                private void UpdateObservableInfos(int id, string novaMensagem)
        {
            var oldItem = Infos.FirstOrDefault(i => i.Id == id);
            var oldIndex = Infos.IndexOf(oldItem);
            Infos[oldIndex].Mensagem = novaMensagem;
        }
        #endregion
    }

    public class Info {

        public int Id { get; set; }
        public string Mensagem { get; set; }
    }

}
