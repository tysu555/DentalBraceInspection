using System;
using System.IO;
using System.Xml.Serialization;
using DentalBraceInspection.Models;

namespace DentalBraceInspection.Services
{
    /// <summary>参数的 XML 持久化。</summary>
    public static class ConfigStore
    {
        public static void Save(string path, InspectionParameters parameters)
        {
            if (parameters == null) return;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var serializer = new XmlSerializer(typeof(InspectionParameters));
            using (var stream = File.Create(path))
            {
                serializer.Serialize(stream, parameters);
            }
        }

        public static InspectionParameters Load(string path)
        {
            if (!File.Exists(path)) return new InspectionParameters();
            try
            {
                var serializer = new XmlSerializer(typeof(InspectionParameters));
                using (var stream = File.OpenRead(path))
                {
                    return (InspectionParameters)serializer.Deserialize(stream);
                }
            }
            catch
            {
                return new InspectionParameters();
            }
        }
    }
}
