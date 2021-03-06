﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using AxesoFeng.Forms;

namespace AxesoFeng.Classes
{
    public partial class BaseFormReader : BaseForm
    {
        protected enum IndexDataView { name = 1, quantity = 2 }
        protected DataView dataView;
        protected MessageComparison messageForm;
        protected ListAssetsForm folioForm;
        protected MenuForm menu;
        protected String folio;
        private bool comparisonSuccesfull;
        protected bool pushComparison;
        protected bool CompSuccesfull
        {
            get { return comparisonSuccesfull; }
        }

        public BaseFormReader(){}

        public void Show(String folio)
        {
            this.folio = folio;
            Show();
        }

        protected void RefreshGrid(ref DataGrid reportGrid)
        {
            Config config = Config.getConfig(menu.pathFolderName + "config.json");
            ProductTable table = new ProductTable();
            Sync sync = new Sync(config.url, menu.idClient, menu.pathFolderName);
            //sync.UpdatedDataBase(menu.rrfid.m_TagTable, menu.products.items);
            foreach (UpcInventory item in menu.rrfid.fillUPCsInventory(menu.products))
            {
                table.addRow(item.upc, item.name, item.total.ToString());
            }
            dataView = new DataView(table);
            reportGrid.DataSource = dataView;
            reportGrid.TableStyles.Clear();
            reportGrid.TableStyles.Add(table.getStyle());
        }

        protected bool ShowMessages(List<string> messages, String valueWarehouse,int Type)
        {
            if (messageForm != null)
            {
                messageForm.fillMessages(messages,valueWarehouse,this.folio,Type);
                messageForm.Show();
                return messageForm.saveDiff;
            }
            return false;
        }

        protected List<RespFolio.Products> ProductsRead()
        {
            List<RespFolio.Products> products = new List<RespFolio.Products>();
            try
            {
                foreach (DataRow row in dataView.Table.Rows)
                {
                    products.Add(new RespFolio.Products(row.ItemArray[(int)IndexDataView.name].ToString(),
                        int.Parse(row.ItemArray[(int)IndexDataView.quantity].ToString())));
                }
            }
            catch (Exception exc) { }
            return products;
        }

        protected void CompareTo(String valueWarehouse,int type)
        {
            Cursor.Current = Cursors.WaitCursor;
            FolioOrder folio = new FolioOrder(menu);
            List<string> messages = new List<string>();
            List<RespFolio.Products> productsRead = ProductsRead();
            //MessageBox.Show("02");
            messages = folio.CompareTo(productsRead, this.folio);
            comparisonSuccesfull = false;
            if (messages.Count == 0)
            {
                if (MessageBox.Show("¿Desea guardar la lectura?", "OK", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
                    messageForm.Save(valueWarehouse, this.folio,type);
                comparisonSuccesfull = true; 
            }
            else
                ShowMessages(messages, valueWarehouse,type);
            Cursor.Current = Cursors.Default;
        }
    }
}