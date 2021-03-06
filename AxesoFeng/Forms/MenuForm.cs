﻿using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using RestSharp;
using System.IO;
using ReadWriteCsv;
using Newtonsoft.Json;
using System.Reflection;
using AxesoFeng.Forms;
using RfidFolio;


namespace AxesoFeng
{
    public partial class MenuForm : BaseForm
    {
        //Child Forms
        public CaptureFolio data;
        public InventoryReportFrm reports;
        public SearchForm search;
        public UPCSearchForm upcsearch;
        public LocateForm locate;
        public OrderExitReportForm orderreport;
        public SyncForm formsync;
        private EditTextForm frmEditText;
        public int idClient = 0;
        public bool showCaptureFolio;

        //RFID Reader
        public SimpleRFID rrfid;

        //Catalogs
        public ProductsList products;
        public Warehouses warehouses;

        //Synchronization
        public Sync sync;

        //Configuration
        public Config configData;
        public FtpConfig ftpConfig;

        public string myResDir;

        public enum typeFolio { loading = 1,unloading = 2}

        public String pathFolderName;

        public MenuForm()
        {
            InitializeComponent();
            //Set Config Data
            pathFolderName = getNameFolderVersion();
            configData = Config.getConfig(pathFolderName + "config.json");
            ftpConfig = FtpConfig.getConfig(pathFolderName + "ftp.json");
            idClient = configData.id_client;
            showCaptureFolio = true;
            //Set Synchronization
            sync = new Sync(configData.url,idClient,pathFolderName);
            sync.conection = sync.GETTest();
            if (sync.conection)
            {                
                sync.GET();
            }
            //Set Reader
            rrfid = new SimpleRFID();
            //rrfid.changeEPC("30342848A80A5AC0000007D9", "30342848A80A5A400001000A");
            //Set Catalogs
            products = new ProductsList(pathFolderName + "products.csv");
            warehouses = new Warehouses(pathFolderName + "warehouses.csv");

            //Set Forms
            reports = new InventoryReportFrm(this);
            search = new SearchForm(this);
            upcsearch = new UPCSearchForm(this,pathFolderName);
            locate = new LocateForm(this);
            formsync = new SyncForm(this);
            orderreport = new OrderExitReportForm(this);
            frmEditText = new EditTextForm(this, pathFolderName + "config.json");
            this.setColors(configData);
            /*
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(@"\rfiddata\img\logo.bmp");
            Image image = new Bitmap(stream);
            LogoPicture.Image = image;
              */

            string myDir = Path.GetDirectoryName(Assembly.GetCallingAssembly().GetName().CodeBase);
            myResDir = Path.Combine(myDir, pathFolderName + "img");
            Image image;
            
            image = new Bitmap(Path.Combine(myResDir, "logo_hqh_med.bmp"));
            LogoPicture.Image = image;
            image = new Bitmap(Path.Combine(myResDir, "menu1.bmp"));
            ReaderPicture.Image = image;
            image = new Bitmap(Path.Combine(myResDir, "menu2.bmp"));
            ReportPicture.Image = image;
            image = new Bitmap(Path.Combine(myResDir, "menu3.bmp"));
            SearchPicture.Image = image;
            image = new Bitmap(Path.Combine(myResDir, "menu4.bmp"));
            SyncPicture.Image = image;
            image = new Bitmap(Path.Combine(myResDir, "menu5_export.bmp"));
            OrderExitPicture.Image = image;
            image = new Bitmap(Path.Combine(myResDir, "menu6.bmp"));
            OrderExitReportPicture.Image = image;
            //image = new Bitmap(Path.Combine(myResDir, "exit.bmp"));
            //ExitPicture.Image = image;
        }

        private string getNameFolderVersion()
        {
            return @"\rfiddata\FOLIO\";
        }

        private void ExitPicture_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void ReaderPicture_Click(object sender, EventArgs e)
        {
            if (configData != null)
            {
                data = new CaptureFolio(this, MenuForm.typeFolio.unloading);
                switch ((Global.Version)configData.version)
                {
                    case Global.Version.ISCAM:
                        data.Show();
                        break;
                    case Global.Version.INVENTORY_PLACE:
                    case Global.Version.INVENTORY:
                        data.NextForm();
                        break;
                }                    
            }
        }

        private void ReportPicture_Click(object sender, EventArgs e)
        {
            reports.Show();
        }

        private void SearchPicture_Click(object sender, EventArgs e)
        {
            search.Show();
        }

        private void SyncPicture_Click(object sender, EventArgs e)
        {            
            formsync.Show();            
            //if (!sync.GET())
            //    return;
            products = new ProductsList(pathFolderName + "products.csv");
            //products_bar = new ProductsList(@"\rfiddata\products_bar.csv");
            warehouses = new Warehouses(pathFolderName + "warehouses.csv");
            if (sync.POSTTrans(formsync, configData.id_user, configData.pwd, configData.id_client))
                MessageBox.Show("Sincronización exitosa", "Sincronización");
            formsync.Hide();
        }

        private void OrderExitReportPicture_Click(object sender, EventArgs e)
        {
            orderreport.Show();
        }

        private void OrderExitPicture_Click(object sender, EventArgs e)
        {
            //orderexit.Show();
            data = new CaptureFolio(this, MenuForm.typeFolio.loading);
            data.Show();
        }

        private void pbClear_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("¿Está seguro de eliminar todas las lecturas?", "Confirmación", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
            FolioOrder.DeleteFiles(pathFolderName);
        }

        private void pbEdit_Click(object sender, EventArgs e)
        {
            frmEditText.Show();
        }

        private void MenuForm_GotFocus(object sender, EventArgs e)
        {
            try { if (frmEditText.Restart)Application.Exit(); }
            catch (Exception exc) { } 
        }

    }
}