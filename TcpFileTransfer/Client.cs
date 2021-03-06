﻿//Rosati-Nicolò Client-TCP file transfer
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using TcpFileTransfer.Models;

namespace TcpFileTransfer
{
    /// <summary>
    /// File transfer over TCP. Client side
    /// </summary>
    public partial class Client : MetroFramework.Forms.MetroForm
    {
        private enum Status { Online, Offline }

        /// <summary>
        /// Initialize the client fields
        /// </summary>
        public Client()
        {
            InitializeComponent();
            ToggleFields(Status.Offline);
            CheckForIllegalCrossThreadCalls = false;
        }

        private readonly string toDownload;
        private ServerManagement ServerManager;

        /// <summary>
        /// Set field's properties based on the given status
        /// </summary>
        /// <param name="s">Given status</param>
        /// <exception cref="InvalidOperationException">Thrown when can't find a given status</exception>
        private void ToggleFields(Status s)
        {
            switch (s)
            {
                case Status.Online:
                    {
                        lblIP.Visible = true;
                        picReload.Enabled = true;
                        btnUpload.Enabled = true;
                        lstBoxFile.Enabled = true;
                        btnDisconnect.Enabled = true;
                        btnConnect.Enabled = false;
                        btnUpload.ForeColor = Color.White;
                        btnUpload.BackColor = SystemColors.MenuHighlight;
                        btnConnect.BackColor = Color.FromArgb(230, 230, 230);
                        btnDisconnect.BackColor = Color.FromArgb(255, 128, 128);
                        break;
                    }
                case Status.Offline:
                    {
                        btnDisconnect.Enabled = false;
                        btnUpload.Enabled = false;
                        lstBoxFile.Enabled = false;
                        picReload.Enabled = false;
                        lblIP.Visible = false;
                        btnConnect.Enabled = true;
                        lstBoxFile.DataSource = null;
                        btnUpload.BackColor = Color.FromArgb(230, 230, 230);
                        btnConnect.BackColor = Color.FromArgb(128, 255, 128);
                        btnDisconnect.BackColor = Color.FromArgb(230, 230, 230);
                        btnUpload.ForeColor = SystemColors.ControlText;
                        break;
                    }
                default:
                    throw new InvalidOperationException("Status not found");
            }
        }

        /// <summary>
        /// Check if an ip address is correct
        /// </summary>
        /// <param name="toCheck">ip to check</param>
        /// <exception cref="ArgumentException">Thrown when the ip address is not correct</exception>
        private void CheckIP(string toCheck)
        {
            if (!IPAddress.TryParse(toCheck, out _))
            {
                throw new ArgumentException("Indirizzo IP non valido");
            }
        }

        /// <summary>
        /// Allows to manage server's connection
        /// </summary>
        private void ManageServer()
        {
            try
            {
                CheckIP(txtIpServer.Text);
                ServerManager = new ServerManagement(txtIpServer.Text);

                ServerManager.SaveFileEvent += ServerManager_SaveFileEvent;

                lstBoxFile.DataSource = ServerManager.ReceiveDirectory();

                lblIP.Text = $"Connesso a : {txtIpServer.Text}";

                ToggleFields(Status.Online);
            }

            catch (ArgumentException) { lblErroreIP.Visible = true; txtIpServer.WithError = true; lblErroreIP.Text = "Indirizzo IP non valido"; }

            catch (SocketException) { MetroFramework.MetroMessageBox.Show(this, "\n\nImpossible contattare il server all'indirizzo: " + txtIpServer.Text, "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error); }

            catch (Exception ex) { MetroFramework.MetroMessageBox.Show(this, "\n\n" + ex.Message, "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ServerManager_SaveFileEvent(object sender, SaveFileEventArgs e)
        {
            string[] fileName = toDownload.Split('\\');
            string[] fileInfo = fileName[fileName.Length - 1].Split('.');

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Seleziona cartella di destinazione";
                sfd.Filter = "Tutti i file (*.*)|*.*";
                sfd.FileName = fileInfo[0];
                sfd.DefaultExt = fileInfo[1];

                DialogResult result = sfd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(sfd.FileName))
                {
                    byte[] toSave = e.received;
                    File.WriteAllBytes(sfd.FileName, toSave);
                }
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            Thread t = new Thread(new ThreadStart(ManageServer))
            {
                IsBackground = true
            };
            lblErroreIP.Visible = false;
            t.Start();
        }

        private void lstBoxFile_MouseClick(object sender, MouseEventArgs e)
        {
            if (!ServerManager.CheckForServerDisconnection())
            {

                ServerManager.RequestFile(toDownload);
            }
            else
            {
                MetroFramework.MetroMessageBox.Show(this, "\n\n" + "Impossibile contattare il server", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ToggleFields(Status.Offline);
            }

        }

        private void lstFileToUpload_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
            {
                e.Effect = DragDropEffects.Move;
                lstFileToUpload.BackColor = Color.FromArgb(230, 230, 230);
            }
        }

        private readonly List<string> dropped = new List<string>();

        private void lstFileToUpload_DragDrop(object sender, DragEventArgs e)
        {
            string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (new FileInfo(items[0]).Length > ServerManagement.SizeToUpload)
            {
                MetroFramework.MetroMessageBox.Show(this, "\n\nSuperato il limite di upload", "Attenzione", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                ServerManagement.SizeToUpload -= new FileInfo(items[0]).Length;
                dropped.AddRange(items.ToList());
                showFileToUpload();
            }
            lstFileToUpload.BackColor = Color.White;
        }

        private void lstFileToUpload_DragLeave(object sender, EventArgs e)
        {
            lstFileToUpload.BackColor = Color.White;
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            try
            {
                ServerManager.UploadFiles(dropped);
                dropped.Clear();
                lstFileToUpload.Items.Clear();
                lstBoxFile.DataSource = ServerManager.ReceiveDirectory();
            }
            catch (ArgumentException ex)
            {
                MetroFramework.MetroMessageBox.Show(this, "\n\n" + ex.Message, "Errore", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (System.IO.IOException)
            {
                MetroFramework.MetroMessageBox.Show(this, "\n\n" + "Impossibile contattare il server", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Update the ListView control with the dropped files
        /// </summary>
        private void showFileToUpload()
        {
            lstFileToUpload.Items.Clear();
            foreach (string data in dropped)
            {
                lstFileToUpload.Items.Add(new ListViewItem { Text = Path.GetFileName(data), ImageIndex = 0 });
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            if (MetroFramework.MetroMessageBox.Show(this, "\n\nInterrompere la connessione al server ?", "Server connesso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                ServerManager.Disconnect();
                lblIP.Text = String.Empty;
                ToggleFields(Status.Offline);
            }
        }

        private void lstFileToUpload_MouseClick(object sender, MouseEventArgs e)
        {
            dropped.RemoveAt(lstFileToUpload.SelectedIndices[0]);
            showFileToUpload();
        }

        private void picReload_Click(object sender, EventArgs e)
        {
            ServerManager.RequestDirectory();
            lstBoxFile.DataSource = ServerManager.ReceiveDirectory();
        }
    }
}
