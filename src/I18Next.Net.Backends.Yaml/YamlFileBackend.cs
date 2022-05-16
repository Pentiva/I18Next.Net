using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using I18Next.Net.TranslationTrees;

namespace I18Next.Net.Backends;

public class YamlFileBackend : ITranslationBackend
{
    private readonly string _basePath;
    private readonly ITranslationTreeBuilderFactory _treeBuilderFactory;

    public YamlFileBackend(string basePath)
        : this(basePath, new GenericTranslationTreeBuilderFactory<HierarchicalTranslationTreeBuilder>())
    {
    }

    public YamlFileBackend(string basePath, ITranslationTreeBuilderFactory treeBuilderFactory)
    {
        _basePath = basePath;
        _treeBuilderFactory = treeBuilderFactory;
    }

    public YamlFileBackend(ITranslationTreeBuilderFactory treeBuilderFactory)
        : this("locales", treeBuilderFactory)
    {
    }

    public YamlFileBackend()
        : this("locales")
    {
    }

    public Encoding Encoding { get; set; } = Encoding.UTF8;

    public Task<ITranslationTree> LoadNamespaceAsync(string language, string @namespace) {
        var path = FindFile(language, @namespace);

        if (path == null)
            return null;

        ExpandoObject parsedYaml;

        var serializer = new SharpYaml.Serialization.Serializer();
        using (var streamReader = new StreamReader(path, Encoding))
            parsedYaml = serializer.Deserialize<ExpandoObject>(streamReader);

        var builder = _treeBuilderFactory.Create();

        PopulateTreeBuilder("", parsedYaml, builder);

        return Task.FromResult(builder.Build());
    }

    private string FindFile(string language, string @namespace)
    {
        var path = Path.Combine(_basePath, language, @namespace + ".yaml");

        if (File.Exists(path))
            return path;

        path = Path.Combine(_basePath, BackendUtilities.GetLanguagePart(language), @namespace + ".yaml");

        return !File.Exists(path) ? null : path;
    }

    private static void PopulateTreeBuilder(string path, IDictionary<string, object> node, ITranslationTreeBuilder builder)
    {
        if (path != string.Empty)
            path += ".";

        foreach (var childNode in node) {
            var key = path + childNode.Key;

            switch (childNode.Value) {
                case ExpandoObject jObj:
                    PopulateTreeBuilder(key, jObj, builder);
                    break;
                case Dictionary<object, object> d:
                    PopulateTreeBuilder(key, d.ToDictionary(a=>a.Key.ToString(), a=>a.Value), builder);
                    break;
                case string jVal:
                    builder.AddTranslation(key, jVal);
                    break;
            }
        }
    }
}
