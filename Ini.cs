using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IniParser
{
    public class Ini
    {
        private readonly string _file;

        private readonly char[] _commentMarks = { '/', ';', '#' };
        private readonly Dictionary<string, Dictionary<string, string>> _content = new Dictionary<string, Dictionary<string, string>>();
        private List<string> _all = new List<string>();

        public Ini(string file)
        {
            _file = file;
        }

        private bool IsCommentedOrBlank(string line)
        {
            return _commentMarks.Any(cm => line.StartsWith(cm.ToString())) || string.IsNullOrWhiteSpace(line);
        }

        private string RemoveCommentChars(string line)
        {
            foreach (var cm in _commentMarks)
                line = line.Trim(cm);

            return line.Trim();
        }

        private static bool IsSection(string line)
        {
            return line.StartsWith("[") && line.EndsWith("]");
        }

        private static string GetSectionName(string line)
        {
            return line.Trim('[').Trim(']').Trim();
        }

        private static KeyValuePair<string, string> GetKeyValuePairFromLine(string line)
        {
            var kv = line.Split(new [] {'='}, 2);
        return new KeyValuePair<string, string>(kv[0].Trim(), kv[1].Trim());
        }

        public string ReadOne(string section, string key, string defaultValue = "")
        {
            if (!File.Exists(_file))
                throw new FileNotFoundException($"Cannot read file {_file} since it does not exists.");

            var sectionFound = false;
            foreach (var line in File.ReadLines(_file))
            {
                var trimmedLine = line.Trim();

                if (IsCommentedOrBlank(trimmedLine)) continue;

                if (!sectionFound)
                {
                    if (!IsSection(line)) continue;
                    var sectionName = GetSectionName(line);
                    if (string.Equals(section, sectionName, StringComparison.Ordinal))
                        sectionFound = true;
                }
                else
                {
                    if (!trimmedLine.Contains('=')) continue;
                    var kv = GetKeyValuePairFromLine(trimmedLine);
                    if (string.Equals(kv.Key, key))
                        return kv.Value;
                }
            }

            return defaultValue;
        }

        public Dictionary<string, Dictionary<string, string>> ReadAll()
        {
            if (!File.Exists(_file))
                throw new FileNotFoundException($"Cannot read file {_file} since it does not exists.");

            var currentSection = string.Empty;
            foreach (var line in File.ReadLines(_file))
            {
                var trimmedLine = line.Trim();

                if (IsCommentedOrBlank(trimmedLine)) continue;

                if (IsSection(trimmedLine))
                {
                    var sectionName = GetSectionName(trimmedLine);
                    _content.Add(sectionName, new Dictionary<string, string>());
                    currentSection = sectionName;
                }

                if (currentSection.Length == 0) continue;
                if (!line.Contains('=')) continue;

                var kv = GetKeyValuePairFromLine(line);
                _content[currentSection].Add(kv.Key, kv.Value);
            }

            return _content;
        }

        public void WriteOneValue(string section, string key, string value)
        {
            var sectionLine = $"[{section}]";
            var kvLine = $"{key}={value}";

            if (File.Exists(_file))
            {
                _all = new List<string>(File.ReadAllLines(_file));
                var sectionIdx = -1;
                var nextSectionIdx = -1;
                var keyIdx = -1;
                for (var i = 0; i < _all.Count; ++i)
                {
                    var uncommentedLine = RemoveCommentChars(_all[i].Trim());

                    if (nextSectionIdx != -1) break;

                    if (IsSection(uncommentedLine))
                    {
                        if (sectionIdx != -1)
                            nextSectionIdx = i;

                        var sectionName = GetSectionName(uncommentedLine);
                        if (string.Equals(section, sectionName, StringComparison.Ordinal))
                            sectionIdx = i;

                        continue;
                    }

                    if (sectionIdx != -1)
                    {
                        if (!uncommentedLine.Contains('=')) continue;
                        var kv = GetKeyValuePairFromLine(uncommentedLine);
                        if (string.Equals(key, kv.Key, StringComparison.Ordinal))
                            keyIdx = i;
                    }
                }

                if (sectionIdx != -1)
                {
                    _all[sectionIdx] = sectionLine;
                    if (keyIdx != -1)
                    {
                        _all[keyIdx] = kvLine;
                    }
                    else
                    {
                        if (nextSectionIdx != -1)
                        {
                            // find the last non-empty line index of this section
                            var insertIndex = nextSectionIdx;
                            for (var i = nextSectionIdx - 1; i != sectionIdx; --i)
                            {
                                if (_all[i].Trim().Length == 0) continue;
                                insertIndex = i;
                                break;
                            }

                            // append to the section
                            _all.Insert(insertIndex + 1, kvLine);
                        }
                        else
                        {
                            if (sectionIdx == _all.Count - 1)
                            {
                                // this [section] is the last line
                                _all.Add(kvLine);
                            }
                            else
                            {
                                // find the last key-value pair index
                                var insertIndex = 0;
                                for (var i = _all.Count - 1; i != sectionIdx; --i)
                                {
                                    if (string.IsNullOrWhiteSpace(_all[i])) continue;
                                    insertIndex = i;
                                    break;
                                }
                                if (insertIndex < _all.Count - 1)
                                    _all.Insert(insertIndex + 1, kvLine);
                                else
                                    _all.Add(kvLine);
                            }
                        }
                    }
                }
                else
                {
                    _all.Add(sectionLine);
                    _all.Add(kvLine);
                }
            }
            else
            {
                _all.Add(sectionLine);
                _all.Add(kvLine);
            }

            File.WriteAllLines(_file, _all);
        }
    }
}
