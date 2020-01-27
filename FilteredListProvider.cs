using System.IO;
using System.Collections.Generic;

namespace OpenFileFromDir
{
    public class FilteredListProvider
    {
        public FilteredListProvider(string rootPath, string[] recentFiles)
        {
            _rootPath = rootPath;
            _recentIndexes = new Dictionary<string, int>();

            if (recentFiles != null)
            {
                for (int i = 0; i < recentFiles.Length; ++i)
                {
                    _recentIndexes[recentFiles[i]] = i;
                }
            }
        }

        public string GetRootPath() { return _rootPath; }

        public void SetFiles(List<string> files)
        {
            _entries = new Entry[files.Count];
            for (int i = 0; i < files.Count; ++i)
            {
                var file = files[i];
                _entries[i].fullPath = file;
                _entries[i].filename = Path.GetFileName(file);
            }
        }

        public struct FilteredEntry
        {
            public string fullPath;
            public string filename;
            public List<int> matchPositions;
            public int sortWeight;

            public enum MatchType
            {
                FileOnly,
                Recent, // also file only
                Path
            }
            public MatchType matchType;
        }

        public List<FilteredEntry> GetFilteredEntries(string filter)
        {
            var ret = new List<FilteredEntry>();

            if (filter.Length == 0)
            {
                // when we have no filter we just return all recent entries
                return ret;
            }

            if (_entries == null) return ret;

            // first find all entries which match
            foreach (var e in _entries)
            {
                var relativePath = e.fullPath.Substring(_rootPath.Length + 1);
                var positions = new List<int>();

                if (Match(relativePath, filter, positions))
                {
                    FilteredEntry fe;
                    fe.fullPath = e.fullPath;
                    fe.filename = e.filename;
                    fe.matchPositions = positions;

                    // sort weight
                    // we want the closer to the beginning a character is, the lower the weight
                    // BUT if a character is part of the filename it gets an even lower weight
                    // so weights before the filename are artificially extended
                    int sortWeight = 0;
                    int filenameStart = relativePath.Length - e.filename.Length;
                    foreach (var index in positions)
                    {
                        if (index >= filenameStart)
                        {
                            sortWeight += index;
                        }
                        else
                        {
                            sortWeight += 1000 + index;
                        }
                    }
                    fe.sortWeight = sortWeight;

                    fe.matchType = FilteredEntry.MatchType.Path;
                    ret.Add(fe);
                }
            }

            // now from all matches find the ones which have a match exclusively in the filename
            for (int i=0; i<ret.Count; ++i)
            {
                var positions = new List<int>();

                var fe = ret[i];

                if (Match(fe.filename, filter, positions))
                {
                    FilteredEntry newfe = fe;
                    newfe.matchPositions = positions;

                    // we need a new match type and a new sorted weight now
                    // the initial sort weight can be what we used before the sum of indices
                    int sortWeight = 0;
                    foreach (var index in positions)
                    {
                        sortWeight += index;
                    }

                    // now check if the entry is in recent
                    if (_recentIndexes.ContainsKey(fe.fullPath))
                    {
                        // force recents on top
                        sortWeight -= 1000;
                        newfe.matchType = FilteredEntry.MatchType.Recent;
                    }
                    else
                    {
                        newfe.matchType = FilteredEntry.MatchType.FileOnly;
                    }
                    newfe.sortWeight = sortWeight;

                    ret[i] = newfe;
                }
            }

            ret.Sort((a, b) => a.sortWeight - b.sortWeight);

            return ret;
        }

        private struct Entry
        {
            public string fullPath; // full path to file
            public string filename; // file name only
        }
        static bool Match(string str, string filter, List<int> positions)
        {
            positions.Clear();
            int istr = 0, iflt = 0;
            while (true)
            {
                if (iflt == filter.Length) return true; // we have a match
                if (istr == str.Length) return false; // string is exhausted - no match

                var cflt = filter[iflt];

                // the filter comes from the user
                // it can contain forward slashes which we want to match with backcslashes
                // here we "lie" that each forward slash is a backward slash
                if (cflt == Path.AltDirectorySeparatorChar) cflt = Path.DirectorySeparatorChar;

                var cstr = str[istr];

                if (char.ToLower(cflt) == char.ToLower(cstr))
                {
                    // match
                    positions.Add(istr);
                    ++iflt;
                    ++istr;
                }
                else
                {
                    // no match continue in str
                    ++istr;
                }
            }
        }

        readonly string _rootPath;
        Entry[] _entries = null;
        Dictionary<string, int> _recentIndexes; // maps a recently used file to its recentness (index in the input array)
    }
}
