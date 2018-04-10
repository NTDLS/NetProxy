using System;
using System.Resources;
using Microsoft.Win32;

namespace NetProxy.Library.Win32
{
    public static class RegistryHelper
    {
        static public void SetString(RegistryKey root, string baseKey, string subKey, string valueName, string value)
        {
            ResourceManager rm = new ResourceManager(typeof(string));
            using (root)
            {
                using (RegistryKey regSubKey = root.OpenSubKey(baseKey + (subKey == null || subKey == string.Empty ? "" : "\\" + subKey), true))
                {
                    if (regSubKey == null)
                    {
                        using (RegistryKey newRegSubKey = root.CreateSubKey(baseKey + (subKey == null || subKey == string.Empty ? "" : "\\" + subKey)))
                        {
                            newRegSubKey.SetValue(valueName, value);
                        }
                    }
                    else
                    {
                        regSubKey.SetValue(valueName, value);
                    }
                }
            }
        }

        static public string GetString(RegistryKey root, string baseKey, string subKey, string valueName)
        {
            return GetString(root, baseKey, subKey, valueName, "");
        }

        static public string GetString(RegistryKey root, string baseKey, string subKey, string valueName, string Default)
        {
            ResourceManager rm = new ResourceManager(typeof(string));
            using (root)
            {
                using (RegistryKey regSubKey = root.OpenSubKey(baseKey + (subKey == null || subKey == string.Empty ? "" : "\\" + subKey)))
                {
                    if (regSubKey != null)
                    {
                        object value = regSubKey.GetValue(valueName);

                        if (value != null)
                        {
                            string stringValue = value.ToString();

                            if (stringValue == null)
                            {
                                return Default;
                            }

                            return stringValue;
                        }
                    }
                }
            }
            return Default;
        }

        static public void SetInt(RegistryKey root, string baseKey, string subKey, string valueName, int value)
        {
            ResourceManager rm = new ResourceManager(typeof(string));
            using (root)
            {
                using (RegistryKey regSubKey = root.OpenSubKey(baseKey + (subKey == null || subKey == string.Empty ? "" : "\\" + subKey), true))
                {
                    if (regSubKey == null)
                    {
                        using (RegistryKey newRegSubKey = root.CreateSubKey(baseKey + (subKey == null || subKey == string.Empty ? "" : "\\" + subKey)))
                        {
                            newRegSubKey.SetValue(valueName, value);
                        }
                    }
                    else
                    {
                        regSubKey.SetValue(valueName, value);
                    }
                }
            }
        }

        static public int GetInt(RegistryKey root, string baseKey, string subKey, string valueName)
        {
            return GetInt(root, baseKey, subKey, valueName, 0);
        }

        static public int GetInt(RegistryKey root, string baseKey, string subKey, string valueName, int Default)
        {
            ResourceManager rm = new ResourceManager(typeof(string));
            using (root)
            {
                using (RegistryKey regSubKey = root.OpenSubKey(baseKey + (subKey == null || subKey == string.Empty ? "" : "\\" + subKey)))
                {
                    if (regSubKey != null)
                    {
                        Object value = regSubKey.GetValue(valueName);

                        if (value == null)
                        {
                            return Default;
                        }

                        return int.Parse(value.ToString());
                    }
                }
            }
            return Default;
        }

        static public void SetBool(RegistryKey root, string baseKey, string subKey, string valueName, bool value)
        {
            SetInt(root, baseKey, subKey, valueName, Convert.ToInt32(value));
        }

        static public bool GetBool(RegistryKey root, string baseKey, string subKey, string valueName)
        {
            return GetInt(root, baseKey, subKey, valueName) != 0;
        }

        static public bool GetBool(RegistryKey root, string baseKey, string subKey, string valueName, bool Default)
        {
            return GetInt(root, baseKey, subKey, valueName, Convert.ToInt32(Default)) != 0;
        }
    }
}
