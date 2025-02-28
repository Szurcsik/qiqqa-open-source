﻿using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace QiqqaLegacyFileFormats          // namespace Qiqqa.Documents.PDF.DiskSerialisation
{

#if SAMPLE_LOAD_CODE

    internal class PDFAnnotationSerializer
    {
        internal static void WriteToDisk(PDFDocument_ThreadUnsafe pdf_document)
        {
            string json = pdf_document.GetAnnotationsAsJSON();
            if (!String.IsNullOrEmpty(json))
            {
                pdf_document.Library.LibraryDB.PutString(pdf_document.Fingerprint, PDFDocumentFileLocations.ANNOTATIONS, json);
            }
        }

        internal static void ReadFromDisk(PDFDocument_ThreadUnsafe pdf_document, ref PDFAnnotationList annotations, Dictionary<string, byte[]> library_items_annotations_cache)
        {
            byte[] annotations_data = null;

            // Try the cache
            if (null != library_items_annotations_cache)
            {
                library_items_annotations_cache.TryGetValue(pdf_document.Fingerprint, out annotations_data);
            }
            else // Try to load the annotations from file if they exist
            {
                var items = pdf_document.Library.LibraryDB.GetLibraryItems(pdf_document.Fingerprint, PDFDocumentFileLocations.ANNOTATIONS);
                if (0 < items.Count)
                {
                    annotations_data = items[0].data;
                }
            }

            // If we actually have some annotations, load them            
            if (null != annotations_data)
            {
                List<DictionaryBasedObject> annotation_dictionaries = null;
                try
                {
                    annotation_dictionaries = ReadFromStream_JSON(annotations_data);
                }
                catch (Exception)
                {
                    annotation_dictionaries = ReadFromStream_BINARY(annotations_data);
                }
            }
        }

        private static List<DictionaryBasedObject> ReadFromStream_JSON(byte[] data)
        {
            string json = Encoding.UTF8.GetString(data);
            List<Dictionary<string, object>> attributes_list = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);

            List<DictionaryBasedObject> annotation_dictionaries = new List<DictionaryBasedObject>();
            foreach (Dictionary<string, object> attributes in attributes_list)
            {
                annotation_dictionaries.Add(new DictionaryBasedObject(attributes));
            }

            return annotation_dictionaries;
        }

        private static List<DictionaryBasedObject> ReadFromStream_BINARY(byte[] data)
        {
            List<DictionaryBasedObject> annotation_dictionaries = (List<DictionaryBasedObject>)SerializeFile.LoadFromByteArray(data);
            return annotation_dictionaries;
        }
    }

#endif

}
