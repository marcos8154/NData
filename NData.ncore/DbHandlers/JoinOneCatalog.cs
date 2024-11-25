namespace NData.DbHandlers
{
    internal class JoinOneCatalog
    {
        public JoinOneCatalog(string propertyName, string alias,
            int resultsFieldListStartPosition)
        {
            PropertyName = propertyName;
            Alias = alias;
            ResultsStartPosition = resultsFieldListStartPosition;
            ResultsEndPosition = resultsFieldListStartPosition;
        }

        public string PropertyName { get; private set; }
        public string Alias { get; private set; }
        public int ResultsStartPosition { get; }
        public int ResultsEndPosition { get; set; }

        public override string ToString() => $"[{PropertyName}] - '{Alias}' - pos[{ResultsStartPosition}...{ResultsEndPosition}]";

    }
}
