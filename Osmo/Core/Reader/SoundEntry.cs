﻿namespace Osmo.Core.Reader
{
    class SoundEntry : SkinningReader
    {
        private string name;
        private bool multipleSounds;
        private string description;

        public string Name => name;

        public bool MultipleSounds => multipleSounds;

        public string Description => description ?? "";

        internal SoundEntry(string line)
        {
            string[] content = ReadLine(line);
            for (int i = 0; i < content.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(content[i]))
                {
                    switch (i)
                    {
                        case 0:
                            name = content[i];
                            break;
                        case 1:
                            description = content[i];
                            break;
                        case 2:
                            multipleSounds = Parser.TryParse(content[i], false);
                            break;
                    }
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})", Name, Description);
        }
    }
}