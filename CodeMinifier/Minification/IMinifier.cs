namespace CodeMinifier.Minification
{
    /// <summary>
    /// A source-code minifier for one language.
    /// </summary>
    public interface IMinifier
    {
        /// <summary>Display name of the language, e.g. "CSS".</summary>
        string Name { get; }

        /// <summary>Returns the minified form of <paramref name="source"/>.</summary>
        string Minify(string source);
    }
}
