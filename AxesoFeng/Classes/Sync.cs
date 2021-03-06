﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using RestSharp;
using ReadWriteCsv;
using System.IO;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using MobileEPC;
using System.Collections;

namespace AxesoFeng
{
    public class Sync
    {

        private RestClient client;
        private int idClient;
        private String pathFolderName;
        public bool conection;

        private class GetObject {
            public List<SyncProduct> products { get; set; }
            public List<SyncWarehouse> warehouses { get; set; }
        }

        //private class GetFolio
        //{
        //    public List<FolioProduct> products { get; set; }
        //}

        //public class FolioProduct
        //{
        //    public String upc { get; set; }
        //    public String name { get; set; }
        //    public int quantity { get; set; }
        //}

        public class SyncProduct {
            public int id { get; set; }
            public String upc { get; set; }
            public String name { get; set; }
            public String description { get; set; }
        }

        private class SyncWarehouse
        {
            public int id { get; set; }
            public String name { get; set; }
            public String description { get; set; }
            public int customer_id { get; set; }
        }

        private class SyncOrdenEsM
        {
            public int customer_id;
            public string date_time;
            public string folio;
            public int type;
            public JsonArray epcs = new JsonArray();
            public int warehouse_id;
            public int client_id;
            
            public enum index
            {
                client_id = 1,
                warehouse_id = 2,
                folio = 3,
                date_time = 4,
            }

            public SyncOrdenEsM(int client_id,string date_time,string folio, int type,int warehouse_id)
            {
                this.client_id = client_id;
                this.customer_id = 0;
                this.date_time = date_time;
                this.folio = folio;
                this.type = type;
                this.warehouse_id = warehouse_id;
            }

        }

        public class SyncOrdenEsD
        {
            public int id;
            public int orden_es_m_id;
            public String epc;
            public int quantity;
            public String created_at;
            public String updated_at;
            public String upc;

            public SyncOrdenEsD(String epc, int quantity)
            {
                String datetime = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"); 
                this.orden_es_m_id = 0;
                this.epc = epc;
                this.quantity = quantity;
                this.updated_at = datetime;
                this.created_at = datetime;
                this.upc = EpcTools.getUpc(epc.ToString());
            }
        }

        private class SyncLog
        {
            public int event_id;
            public int user_id;
            public int type;
            public String description;
            public String date_time;
            public int client_id;
            public String folio;
        }

        public Sync(String BaseUrl, int idClient, String pathFolderName)
        {
            client = new RestClient(BaseUrl);
            this.idClient = idClient;
            this.pathFolderName = pathFolderName;
        }

        public bool GETTest()
        {
            var request = new RestRequest("test_conection", Method.GET);
            IRestResponse response = client.Execute(request);
            String text = response.Content;
            if (!requestError(response.StatusCode.ToString()))
                return false;
            return true;
        }

        public bool GET()
        {
            var request = new RestRequest("sync_data", Method.POST);
            JsonObject json = new JsonObject();

            request.RequestFormat = DataFormat.Json;
            json.Add("pclient_id",idClient.ToString());
            request.AddBody(json);

            IRestResponse<GetObject> response = client.Execute<GetObject>(request);
            GetObject data = response.Data;

            if (!requestError(response.StatusCode.ToString()))
                return false;

            Directory.CreateDirectory(pathFolderName);

            using (CsvFileWriter writer = new CsvFileWriter(pathFolderName + "products.csv"))
            {                
                foreach (SyncProduct item in data.products)
                {
                    CsvRow row = new CsvRow();
                    row.Add(item.upc);
                    row.Add(item.name);
                    writer.WriteRow(row);
                }
            }

            using (CsvFileWriter writer = new CsvFileWriter(pathFolderName + "warehouses.csv"))
            {
                foreach (SyncWarehouse item in data.warehouses)
                {
                    CsvRow row = new CsvRow();
                    row.Add(item.id.ToString());
                    row.Add(item.name);
                    writer.WriteRow(row);
                }
            }            
            return true;
        }

        public bool POSTTrans(SyncForm sync, int idUser, string pwd, int idClient)
        {
            Directory.CreateDirectory(pathFolderName);
            string[] filePaths = Directory.GetFiles(pathFolderName);
            List<string> upcFiles = new List<string>();
            List<string> epcFiles = new List<string>();
            List<string> messages = new List<string>();
            int numInputs = 0;
            int numOutputs = 0;

            foreach (String path in filePaths)
            {
                if (path.Contains("epc"))
                    epcFiles.Add(path);
                if (path.Contains("message"))
                    messages.Add(path);
                if (path.Contains("iepc"))
                    numInputs++;
                if (path.Contains("oepc"))
                    numOutputs++;
            }

            sync.updateInputs(numInputs.ToString() + " Inventarios");
            sync.updateOutputs(numOutputs.ToString() + " Salidas");
            Application.DoEvents();

            foreach (String path1 in epcFiles)
            {
                var request = new RestRequest("sync", Method.POST);
                request.RequestFormat = DataFormat.Json;
                request.AddBody(buildPOSTRequest(path1, ""));
                IRestResponse response = client.Execute(request);
                if (!requestError(response.StatusCode.ToString()))
                    return false;
                
                Application.DoEvents();
                String nameFileMessage = path1.Replace("iepcs", "message").Replace("oepcs", "message");
                File.Move(nameFileMessage, nameFileMessage.Replace("rfiddata", "rfiddataold"));
                File.Move(path1, path1.Replace("rfiddata", "rfiddataold"));
                File.Move(path1.Replace("epc", "upc"), path1.Replace("rfiddata", "rfiddataold").Replace("epc", "upc"));
                if (path1.Contains("iepc")){
                    numInputs--;
                    sync.updateInputs(numInputs.ToString() + " Entradas");                    
                }
                else if (path1.Contains("oepc")){
                    numOutputs--;
                    sync.updateOutputs(numOutputs.ToString() + " Salidas");                   
                }
            }
            return true;
        }

        private JsonObject buildPOSTRequest(String path, String path1)
        {
            var inventories = new JsonArray();

            if (path != "")
                inventories.Add(buildInventory(path));

            String nameFileMessage = path.Replace("iepcs", "message").Replace("oepcs", "message");

            //var messages = new JsonArray();

            //messages.Add(LogToJson(deserealizeNameFileLog(nameFileMessage)));

            var json = new JsonObject();
            json.Add("inventories", inventories);
            //json.Add("messages", messages);

            return json;
        }

        private JsonObject buildInventory(String path1)
        {
            string messages;
            var inventory = new JsonObject();
            var epcs = new JsonArray();
            SyncOrdenEsM OrderM = deserealizeNameFile(path1);
            using (CsvFileReader reader = new CsvFileReader(path1))
            {
                CsvRow rowcsv = new CsvRow();
                while (reader.ReadRow(rowcsv))
                {
                    epcs.Add(rowcsv[0]);
                }
            }
            messages = buildMessages(path1);
            inventory.Add("client_id", idClient);
            inventory.Add("user_id", 1);
            inventory.Add("created_at", OrderM.date_time);
            inventory.Add("updated_at", OrderM.date_time);
            inventory.Add("folio", OrderM.folio);
            inventory.Add("type", OrderM.type);
            inventory.Add("warehouse_id", OrderM.warehouse_id);
            inventory.Add("pending", 0);
            inventory.Add("handheld", 1);
            inventory.Add("epcs", epcs);
            inventory.Add("messages", messages);
            return inventory;

        }

        private string buildMessages(String path)
        {
            string messages = "";
            String nameFileMessage = path.Replace("iepcs", "message").Replace("oepcs", "message");
            using (CsvFileReader reader = new CsvFileReader(nameFileMessage))
            {
                CsvRow rowcsv = new CsvRow();
                while (reader.ReadRow(rowcsv))
                {
                    messages = messages + "," + rowcsv[0];
                }
            }
            return messages;
        }

        private bool requestError(String StatusCode)
        {            
            switch (StatusCode)
            {
                case "0":
                case "NotFound":
                    MessageBox.Show("Error de Red. No se pudieron sincronizar los datos con el servidor. Verifique que tiene su wifi encendida, que tiene acceso a la red y al servidor.", "Error");
                    Cursor.Current = Cursors.Default;
                    return false;
                case "Forbidden":
                case "InternalServerError":
                    MessageBox.Show("Error contacte a su administrador.", "Error");
                    Cursor.Current = Cursors.Default;
                    return false;
                case "OK":
                    return true;
                default:
                    MessageBox.Show(StatusCode, "Error");
                    Cursor.Current = Cursors.Default;
                    return false;
            }
        }

        /// <summary>
        /// deserealize the name of a file for create an object OrdenEsM
        /// </summary>
        /// <param name="path"></param>
        /// 1(idCustomer)_1(type)_12345(folio)_1405301435(datetime)</param>
        /// <returns></returns>
        /// 
        private SyncOrdenEsM deserealizeNameFile(string path)
        {
            //1_1_1234124412_14-11-29-013045
            string[] comp = path.Split(new Char[] { '_'});
            SyncOrdenEsM orden = null;
            try
            {
                if (comp.Length <= 6)
                {
                    orden = new SyncOrdenEsM(int.Parse(comp[(int)SyncOrdenEsM.index.client_id]),
                        FormatDateTime(comp[(int)SyncOrdenEsM.index.date_time].Replace(".csv", "")),
                        comp[(int)SyncOrdenEsM.index.folio],
                        Type(comp[0].Replace(pathFolderName, "")[0]),
                        int.Parse(comp[(int)SyncOrdenEsM.index.warehouse_id]));
                }
                else
                {
                    orden = new SyncOrdenEsM(int.Parse(comp[(int)SyncOrdenEsM.index.client_id]),
                        FormatDateTime(comp[(int)SyncOrdenEsM.index.date_time+2].Replace(".csv", "")),
                        comp[(int)SyncOrdenEsM.index.folio] + "_" +comp[(int)SyncOrdenEsM.index.folio+1] + "_"+ comp[(int)SyncOrdenEsM.index.folio+2],
                        Type(comp[0].Replace(pathFolderName, "")[0]),
                        int.Parse(comp[(int)SyncOrdenEsM.index.warehouse_id]));
                }
            }
            catch (Exception exc) { }
            return orden;
        }

        private SyncLog deserealizeNameFileLog(string path)
        {
            //1_1_1234124412_14-11-29-013045
            string[] comp = path.Split(new Char[] { '_' });
            SyncLog log = null;
            try
            {
                log = new SyncLog();
                log.client_id = int.Parse(comp[(int)SyncOrdenEsM.index.client_id]);
                if (comp.Length <= 6){
                    log.date_time = FormatDateTime(comp[(int)SyncOrdenEsM.index.date_time].Replace(".csv", ""));
                    log.folio = comp[(int)SyncOrdenEsM.index.folio];
                }
                else {
                    log.date_time = FormatDateTime(comp[(int)SyncOrdenEsM.index.date_time+2].Replace(".csv", ""));
                    log.folio = comp[(int)SyncOrdenEsM.index.folio] + "_" + 
                        comp[(int)SyncOrdenEsM.index.folio + 1] + "_" + comp[(int)SyncOrdenEsM.index.folio + 2];
                }
                //log.type = Type(comp[0].Replace("\\rfiddata\\", "")[0]);
                log.type = Type(comp[0].Replace("\\" + pathFolderName, "")[0]);
                log.user_id = 1;
                log.description = ContentFile(path);
            }
            catch (Exception exc) { }
            return log;
        }

        private String ContentFile(string path)
        {
            string[] comp = path.Split(new Char[] { '_' });
            String content = "";
            try
            {
                String folio = comp[(int)SyncOrdenEsM.index.folio];
                using (StreamReader sr = new StreamReader(path))
                {
                    content = sr.ReadToEnd();
                    content = "folio = " + folio +","+ content;
                }
            }
            catch (Exception exc)
            { }
            return content;
        }

        private int Type(char cType)
        {
            int iType = -1; 
            if(cType == 'i')
                iType = 1;
            else if(cType == 'o')
                iType = 0;
            return iType;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dateTime">format example 1405301435</param>
        /// <returns>string format 2014-12-31 20:30:35</returns>
        public static string FormatDateTime(String dateTime)
        {
            string[] dt = dateTime.Split(new Char[] { '-'});
            string dateTimeF = "20" + dt[0] + "-" + dt[1] + "-" + dt[2] + " " + 
                dt[3].Substring(0, 2) + ":" + dt[3].Substring(2, 2) + ":" + dt[3].Substring(4, 2);
            return dateTimeF;
        }

        //internal void UpdatedDataBase(Hashtable productsRead, List<Product> productlist)
        //{
        //    List<String> productsUnre = ListUPCUnrecognized(productsRead, productlist);
        //    SyncProduct prodResp;
        //    foreach (String product in productsUnre)
        //    {
        //        prodResp = GETProduct(product);
        //        AddProductDataBase(prodResp);
        //    }
        //}

        //private bool AddProductDataBase(SyncProduct product)
        //{
        //    using (CsvFileWriter writer = new CsvFileWriter(pathFolderName + "productsrfid.csv"))
        //    {
        //        CsvRow row = new CsvRow();
        //        row.Add(product.upc);
        //        row.Add(product.name);
        //        writer.WriteRow(row);
        //    }
        //    var request = new RestRequest("add_product", Method.POST);
        //    request.RequestFormat = DataFormat.Json;
        //    request.AddBody(product);
        //    IRestResponse response = client.Execute(request);
        //    if (!requestError(response.StatusCode.ToString()))
        //        return false;
        //    if (!response.Content.Equals("yes save"))
        //        return false;
        //    return true;
        //}

        private List<String> ListUPCUnrecognized(Hashtable productsRead, List<Product> productlist) 
        {
            Boolean find;
            List<String> productsUnr = new List<String>();
            String epc;
            foreach (DictionaryEntry productr in productsRead)
            {
                find = false;
                //product.Key, product.Value
                foreach (Product productl in productlist)
                {
                    epc = EpcTools.getUpc(productr.Key.ToString());
                    if (productl.upc == epc)
                    { find = true;  break;}
                }
                if (find == false)
                    productsUnr.Add(productr.Key.ToString());
            }
            return productsUnr;
        }

        public SyncProduct GETProduct(String epc)
        {
            var request = new RestRequest("test_get_product", Method.POST);
            IRestResponse<SyncProduct> response = client.Execute<SyncProduct>(request);
            SyncProduct product = response.Data;

            if (!requestError(response.StatusCode.ToString()))
                return null;

            return product;
        }

    }
}
