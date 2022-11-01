﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ApartmentLayouts
{
    public class ApartmentLayoutsSettings
    {
        public string ApartmentLayoutsSettingsValue { get; set; }
        public static ApartmentLayoutsSettings GetSettings()
        {
            ApartmentLayoutsSettings apartmentLayoutsSettings = null;
            string assemblyPathAll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string fileName = "ApartmentLayoutsSettings.xml";
            string assemblyPath = assemblyPathAll.Replace("ApartmentLayouts.dll", fileName);

            if (File.Exists(assemblyPath))
            {
                using (FileStream fs = new FileStream(assemblyPath, FileMode.Open))
                {
                    XmlSerializer xSer = new XmlSerializer(typeof(ApartmentLayoutsSettings));
                    apartmentLayoutsSettings = xSer.Deserialize(fs) as ApartmentLayoutsSettings;
                    fs.Close();
                }
            }
            else
            {
                apartmentLayoutsSettings = new ApartmentLayoutsSettings();
            }

            return apartmentLayoutsSettings;
        }

        public void SaveSettings()
        {
            string assemblyPathAll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string fileName = "ApartmentLayoutsSettings.xml";
            string assemblyPath = assemblyPathAll.Replace("ApartmentLayouts.dll", fileName);

            if (File.Exists(assemblyPath))
            {
                File.Delete(assemblyPath);
            }

            using (FileStream fs = new FileStream(assemblyPath, FileMode.Create))
            {
                XmlSerializer xSer = new XmlSerializer(typeof(ApartmentLayoutsSettings));
                xSer.Serialize(fs, this);
                fs.Close();
            }
        }
    }
}