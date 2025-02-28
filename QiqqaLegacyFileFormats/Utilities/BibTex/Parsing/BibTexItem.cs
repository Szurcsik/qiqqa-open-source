﻿using System;
using System.Collections.Generic;
using System.Text;

namespace QiqqaLegacyFileFormats          // namespace Utilities.BibTex.Parsing
{
    /// <summary>
    /// A completely parsed BibTeX record.
    /// </summary>
    public class BibTexItem
    {
        public string Type;
        public string Key;

        public List<string> Exceptions = new List<string>();
        public List<string> Warnings = new List<string>();

        private Dictionary<string, string> fields = new Dictionary<string, string>();

        public override string ToString()
        {
            return String.Format("{0}({1}) - {2} fields", Type, Key, fields.Count);
        }

        // Wants to look like the below:

        /*
            @ARTICLE{article-full,
                author = {L[eslie] A. Aamport},
                title = {The Gnats and Gnus Document Preparation System},
                journal = {\mbox{G-Animal's} Journal},
                year = 1986,
                volume = 41,
                number = 7,
                pages = "73+",
                month = jul,
                note = "This is a full ARTICLE entry",
            }
         */
        public string ToBibTex()
        {
            StringBuilder sb = new StringBuilder();

            //...

            return sb.ToString();
        }

        public void SetIfHasValue(string index, string value)
        {
            if (!String.IsNullOrWhiteSpace(value))
            {
                this[index] = value;
            }
        }

        public string this[string index]
        {
            set
            {
                index = index.ToLower();
                fields[index] = value;
            }

            get
            {
                index = index.ToLower();

                string result;
                if (fields.TryGetValue(index, out result))
                {
                    return result;
                }
                else
                {
                    return "";
                }
            }
        }

        public IEnumerable<KeyValuePair<string, string>> Fields => fields;

        [Newtonsoft.Json.JsonIgnore]    // https://stackoverflow.com/questions/10169648/how-to-exclude-property-from-json-serialization#answer-25566387
        public Dictionary<string, string>.KeyCollection FieldKeys => fields.Keys;

        public string GetExceptionsAndMessagesString()
        {
            return GetExceptionsString() + GetWarningsString();
        }

        public string GetExceptionsString()
        {
            return GetMessagesString(Exceptions, "Errors:");
        }

        public string GetWarningsString()
        {
            return GetMessagesString(Warnings, "Warnings:");
        }

        private static string GetMessagesString(List<string> messages, string header)
        {
            if (messages.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine(header);
                sb.AppendLine();
                foreach (string s in messages)
                {
                    sb.Append("  ");
                    sb.AppendLine(s);
                }
                return sb.ToString();
            }
            else
            {
                return "";
            }
        }

        public bool ContainsField(string field)
        {
            return fields.ContainsKey(field.ToLower());
        }

        public bool RemoveKey(string key)
        {
            return fields.Remove(key);
        }
    }
}
