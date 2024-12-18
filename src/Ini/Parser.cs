using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ini
{
    public class Parser
    {
        public char CommentCharForWriting { get; set; } = ';';

        public readonly List<char> CommentChars = new List<char> { ';', '/' };

        private static readonly Regex SectionRegex = new Regex(@"^\[([^\]]+?)\]");

        private readonly List<string> _lines = new List<string>();
        private readonly List<SectionInfo> _sections = new List<SectionInfo>();

        private readonly string _filename;
        private readonly StringComparison _comparison;

        public Parser(string filename, StringComparison comparison = StringComparison.Ordinal)
        {
            _filename = filename;
            _comparison = comparison;

            if (File.Exists(_filename))
            {
                _lines = File.ReadAllLines(filename).ToList();
                SetSections();
            }
        }

        private void Save()
        {
            SetSections();
            File.WriteAllLines(_filename, _lines);
        }

        private bool IsCommented(string line)
        {
            return CommentChars.Any(c => line.Trim().StartsWith(c.ToString()));
        }

        private string TrimLine(string origin)
        {
            return origin.Trim().TrimStart(CommentChars.ToArray()).Trim();
        }

        private struct SectionInfo
        {
            public int FileIndex { get; set; }
            public bool IsCommented { get; set; }
            public string Name { get; set; }
        }

        private void SetSections()
        {
            _sections.Clear();

            for (var i = 0; i < _lines.Count; ++i)
            {
                var line = TrimLine(_lines[i]);
                var sectionName = SectionRegex.Match(line);
                if (sectionName.Success)
                {
                    _sections.Add(new SectionInfo
                    {
                        Name = sectionName.Groups[1].Value.Trim(),
                        FileIndex = i,
                        IsCommented = IsCommented(_lines[i])
                    });
                }
            }
        }

        private int GetSectionInfoIndex(string sectionName)
        {
            for (var i = 0; i < _sections.Count; ++i)
                if (string.Equals(sectionName, _sections[i].Name, _comparison))
                    return i;

            return -1;
        }

        private int GetNextSectionFileIndex(string sectionName)
        {
            // is this the last section in the file?
            var endIndex = _lines.Count - 1;

            var sectionInfoIndex = GetSectionInfoIndex(sectionName);

            if (sectionInfoIndex == _sections.Count - 1)
                return endIndex;

            return _sections[sectionInfoIndex + 1].FileIndex;
        }

        private struct KeyInfo
        {
            public int FileIndex;
            public bool IsCommented;
            public string Name;
            public string Value;
        }

        private KeyInfo GetKeyInfo(SectionInfo sectionInfo, string key)
        {
            var endIndex = GetNextSectionFileIndex(sectionInfo.Name);
            var keyInfo = new KeyInfo { FileIndex = -1 };

            // find the key
            for (var i = sectionInfo.FileIndex + 1; i <= endIndex; ++i)
            {
                if (string.IsNullOrWhiteSpace(_lines[i])) continue;

                var kvLine = TrimLine(_lines[i]);
                var kv = kvLine.Split(new[] { '=' }, 2);
                if (kv.Length != 2) continue;

                var k = kv[0].Trim();
                if (string.Equals(key, k, _comparison))
                {
                    keyInfo.Name = key;
                    keyInfo.FileIndex = i;
                    keyInfo.Value = kv[1];
                    keyInfo.IsCommented = IsCommented(_lines[i]);

                    return keyInfo;
                }
            }

            return keyInfo;
        }

        public bool TryGetValue(string section, string key, out string value)
        {
            if (!File.Exists(_filename))
            {
                value = null;
                return false;
            }

            var sectionInfoIndex = GetSectionInfoIndex(section);
            if (sectionInfoIndex == -1)
            {
                value = null;
                return false;
            }

            var sectionInfo = _sections[sectionInfoIndex];
            if (sectionInfo.IsCommented)
            {
                value = null;
                return false;
            }

            var keyInfo = GetKeyInfo(sectionInfo, key);
            if (keyInfo.FileIndex == -1)
            {
                value = null;
                return false;
            }

            if (keyInfo.IsCommented)
            {
                value = null;
                return false;
            }

            value = keyInfo.Value;
            return true;
        }

        public string GetValue(string section, string key)
        {
            if (TryGetValue(section, key, out var value))
                return value;

            if (!File.Exists(_filename))
                throw new FileNotFoundException($"File {_filename} does not exists.");

            var errorMessage = $"There is no value of {key} in section {section}.";
            throw new KeyNotFoundException(errorMessage);
        }

        public void WriteValue(string section, string key, string value)
        {
            // check if section exists
            var secIdx = GetSectionInfoIndex(section);
            if (secIdx == -1)
            {
                string[] newLines = { $"[{section}]", $"{key} = {value}" };
                _lines.AddRange(newLines);

                Save();
                return;
            }

            // section exists
            var secInfo = _sections[secIdx];
            if (secInfo.IsCommented)
            {
                // but commented, undo it
                _lines[secInfo.FileIndex] = $"[{section}]";
            }

            // check if key exists
            var keyInfo = GetKeyInfo(secInfo, key);
            var newLine = $"{key} = {value}";
            if (keyInfo.FileIndex == -1)
            {
                // key does not exist
                var idx = GetNextSectionFileIndex(section);

                if (idx > 0)
                {
                    if (idx == _lines.Count - 1)
                    {
                        _lines.Add(newLine);
                        Save();
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(_lines[idx - 1]))
                        --idx;
                }

                _lines.Insert(idx, newLine);

                Save();
                return;
            }

            // no need to care about the key is commented or not here
            _lines[keyInfo.FileIndex] = newLine;

            Save();
        }

        public void CommentKey(string section, string key)
        {
            if (!File.Exists(_filename)) return;

            var secIdx = GetSectionInfoIndex(section);
            if (secIdx == -1) return;
            var secInfo = _sections[secIdx];
            if (secInfo.IsCommented) return;

            var keyInfo = GetKeyInfo(secInfo, key);
            if (keyInfo.FileIndex == -1) return;
            if (keyInfo.IsCommented) return;

            _lines[keyInfo.FileIndex] = $"{CommentCharForWriting} {keyInfo.Name} = {keyInfo.Value}";

            var endIdx = GetNextSectionFileIndex(section);
            var noKeyLeft = true;
            for (var i = secInfo.FileIndex + 1; i <= endIdx; ++i)
            {
                var l = _lines[i];
                if (string.IsNullOrWhiteSpace(l))
                    continue;

                var rs = CommentChars.Select(c => l.StartsWith(c.ToString()));
                if (!rs.Contains(true))
                {
                    // if the key-value pair is at the end of the file
                    if (SectionRegex.IsMatch(l))
                        continue;

                    noKeyLeft = false;
                    break;
                }
            }

            if (noKeyLeft)
                _lines[secInfo.FileIndex] = $"{CommentCharForWriting} [{section}]";

            Save();
        }
    }
}
