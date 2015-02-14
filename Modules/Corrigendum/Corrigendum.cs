using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using VBCBBot;

namespace Corrigendum
{
    /// <summary>
    /// Corrects spelling mistakes, especially deliberate ones.
    /// </summary>
    public class Corrigendum : ModuleV1
    {
        private readonly CorrigConfig _config;
        private readonly Dictionary<string, HashSet<string>> _wordLists;

        public Corrigendum(ChatboxConnector connector, JObject config)
            : base(connector)
        {
            _config = new CorrigConfig(config);
            _wordLists = new Dictionary<string, HashSet<string>>();

            // read the dictionaries
            foreach (var item in _config.Items)
            {
                if (_wordLists.ContainsKey(item.WordListFilename))
                {
                    // done already
                    continue;
                }

                using (var dictFile = new StreamReader(item.WordListFilename, Encoding.UTF8))
                {
                    var wordSet = new HashSet<string>();

                    string line;
                    while ((line = dictFile.ReadLine()) != null)
                    {
                        var trimmedLine = line.Trim().ToLower();
                        if (trimmedLine.Length > 0)
                        {
                            wordSet.Add(trimmedLine);
                        }
                    }

                    _wordLists[item.WordListFilename] = wordSet;
                }
            }
        }

        private string RemoveNonWord(string str)
        {
            var ret = new StringBuilder();
            foreach (var c in str)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (ret.Length > 0 && ret[ret.Length - 1] != ' ')
                    {
                        ret.Append(' ');
                    }
                }
                else if (char.IsLetterOrDigit(c))
                {
                    ret.Append(c);
                }
            }
            if (ret[ret.Length - 1] == ' ')
            {
                ret.Remove(ret.Length - 1, 1);
            }
            return ret.ToString();
        }

        protected override void ProcessUpdatedMessage(ChatboxMessage message, bool isPartOfInitialSalvo = false, bool isEdited = false, bool isBanned = false)
        {
            if (isPartOfInitialSalvo || isEdited || isBanned)
            {
                return;
            }

            if (message.UserName == Connector.ForumConfig.Username)
            {
                return;
            }

            var body = RemoveNonWord(message.Body);
            var words = body.Split(' ');

            var correctedWords = new List<string>();
            foreach (var item in _config.Items)
            {
                var wordSet = _wordLists[item.WordListFilename];

                foreach (var word in words)
                {
                    var lowercaseWord = word.ToLower();

                    // word must contain the "from" substring
                    if (lowercaseWord.IndexOf(item.From) == -1)
                    {
                        // doesn't
                        continue;
                    }

                    // is the word in the dictionary?
                    if (wordSet.Contains(lowercaseWord))
                    {
                        // it is
                        continue;
                    }

                    // correct the word
                    var corrected = word.Replace(item.From, item.To);
                    var correctedLower = corrected.ToLower();

                    // is the corrected word in the dictionary?
                    if (wordSet.Contains(correctedLower))
                    {
                        // yes; format and add it to the corrected words list
                        correctedWords.Add(string.Format(_config.CorrectedWordFormat, corrected));
                    }
                }
            }

            if (correctedWords.Count == 0)
            {
                return;
            }

            var joined = string.Join(_config.Separator, correctedWords);
            Connector.SendMessage(joined);
        }
    }
}
