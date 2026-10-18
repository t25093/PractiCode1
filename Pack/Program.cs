using System.CommandLine;
using System.ComponentModel.DataAnnotations;

var languageExtensions = new Dictionary<string, string[]>
{
    ["cs"] = new[] { ".cs" },
    ["js"] = new[] { ".js" },
    ["py"] = new[] { ".py" },
    ["java"] = new[] { ".java" },
    ["cpp"] = new[] { ".cpp", ".h", ".hpp" }
};
string[] GetSourceFiles(string rootDirectory)
{
    var excludedDirectories = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "debug",
        ".git"
    };

    return Directory
        .EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories)
        .Where(file =>
            !file.Split(Path.DirectorySeparatorChar)
                .Any(part => excludedDirectories.Contains(part)))
        .ToArray();
}

string[] FilterByLanguage(
    string[] files,
    string[] languages)
{
    if (languages.Contains("all", StringComparer.OrdinalIgnoreCase))
    {
        return files
            .Where(file => languageExtensions.Values
                .SelectMany(extensions => extensions)
                .Contains(
                    Path.GetExtension(file),
                    StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    var extensionsToInclude = languages
        .SelectMany(language => languageExtensions[language])
        .ToArray();

    return files
        .Where(file => extensionsToInclude.Contains(
            Path.GetExtension(file),
            StringComparer.OrdinalIgnoreCase))
        .ToArray();
}

var outputOption = new Option<FileInfo>("--output", "-o")
{
    Description = "File path and name"
};

var languageOption = new Option<string[]>("--language", "-l")
{
    Description = "Programming languages to include, or 'all'",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};

languageOption.Validators.Add(result =>
{
    var languages = result.GetValue(languageOption);

    if (languages == null || languages.Length == 0)
    {
        result.AddError("Please specify at least one language.");
        return;
    }

    var supportedLanguages = new[] { "cs", "js", "py", "java", "cpp" };

    if (!languages.Contains("all") &&
        languages.Any(language => !supportedLanguages.Contains(language)))
    {
        result.AddError(
            "Unsupported language. Use cs, js, py, java, cpp, or all.");
    }
});

var sortOption = new Option<string>("--sort", "-s")
{
    Description = "Sort files by name or type",
    DefaultValueFactory = _ => "name"
};

sortOption.Validators.Add(result =>
{
    var value = result.GetValue(sortOption);

    if (value != "name" && value != "type")
    {
        result.AddError("Sort must be 'name' or 'type'.");
    }
});

var noteOption = new Option<bool>("--note", "-n")
{
    Description = "Include source file name and relative path as a comment"
};

var removeEmptyLinesOption = new Option<bool>(
    "--remove-empty-lines", "-r")
{
    Description = "Remove empty lines from source files"
};

var authorOption = new Option<string?>("--author", "-a")
{
    Description = "Name of the bundle author"
};

var bundleCommand = new Command("bundle", "Bundle code files into a single file");


bundleCommand.SetAction(parseResult =>
{
    var output = parseResult.GetValue(outputOption);
    var languages = parseResult.GetValue(languageOption);
    var sort = parseResult.GetValue(sortOption);
    var note = parseResult.GetValue(noteOption);
    var removeEmptyLines = parseResult.GetValue(removeEmptyLinesOption);
    var author = parseResult.GetValue(authorOption);

    var rootDirectory = Directory.GetCurrentDirectory();
    var files = GetSourceFiles(rootDirectory);
    var selectedLanguages = languages ?? Array.Empty<string>();

    var filteredFiles = FilterByLanguage(files, selectedLanguages);

    if (output != null)
    {
        var outputFullPath = Path.GetFullPath(output.FullName);
        filteredFiles = filteredFiles
            .Where(file => !string.Equals(
                Path.GetFullPath(file),
                outputFullPath,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    Console.WriteLine($"Found {filteredFiles.Length} matching files.");

    if (filteredFiles.Length == 0)
    {
        Console.WriteLine("No matching source files were found.");
        return;
    }

    if (output == null)
    {
        Console.WriteLine("Please specify an output file using --output.");
        return;
    }

    var orderedFiles = sort == "type"
        ? filteredFiles
            .OrderBy(file => Path.GetExtension(file),
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => Path.GetFileName(file),
                StringComparer.OrdinalIgnoreCase)
            .ToArray()
        : filteredFiles
            .OrderBy(file => Path.GetFileName(file),
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

    var outputPath = Path.GetFullPath(output.FullName);

    var bundleContent = new System.Text.StringBuilder();

    if (!string.IsNullOrWhiteSpace(author))
    {
        bundleContent.AppendLine($"// Author: {author}");
        bundleContent.AppendLine();
    }

    foreach (var file in orderedFiles)
    {
        if (note)
        {
            var relativePath = Path.GetRelativePath(rootDirectory, file);
            bundleContent.AppendLine($"// File: {Path.GetFileName(file)}");
            bundleContent.AppendLine($"// Path: {relativePath}");
        }

        var content = File.ReadAllText(file);

        if (removeEmptyLines)
        {
            content = string.Join(
                Environment.NewLine,
                content.Split(
                    new[] { "\r\n", "\n", "\r" },
                    StringSplitOptions.None)
                .Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        bundleContent.AppendLine(content);
        bundleContent.AppendLine();
    }

    File.WriteAllText(outputPath, bundleContent.ToString());

    Console.WriteLine($"Bundle created successfully: {outputPath}");

    Console.WriteLine($"Output: {output}");
    Console.WriteLine($"Languages: {string.Join(", ", languages ?? Array.Empty<string>())}");
    Console.WriteLine($"Sort: {sort}");
    Console.WriteLine($"Note: {note}");
    Console.WriteLine($"Remove empty lines: {removeEmptyLines}");
    Console.WriteLine($"Author: {author}");
});

bundleCommand.Options.Add(outputOption);
bundleCommand.Options.Add(languageOption);
bundleCommand.Options.Add(sortOption);
bundleCommand.Options.Add(noteOption);
bundleCommand.Options.Add(removeEmptyLinesOption);
bundleCommand.Options.Add(authorOption);

var createRspCommand = new Command(
    "create-rsp",
    "Create a response file interactively");

createRspCommand.SetAction(parseResult =>
{
    Console.Write("Enter response file name (for example: bundle.rsp): ");
    var responseFileName = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(responseFileName))
    {
        Console.WriteLine("Response file name cannot be empty.");
        return;
    }

    Console.Write("Enter languages (cs, js, py, java, cpp, or all): ");
    var languages = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(languages))
    {
        Console.WriteLine("Language cannot be empty.");
        return;
    }

    Console.Write("Enter output file path (for example: bundle.txt): ");
    var outputPath = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(outputPath))
    {
        Console.WriteLine("Output path cannot be empty.");
        return;
    }

    Console.WriteLine($"Languages: {languages}");
    Console.WriteLine($"Output: {outputPath}");

    Console.Write("Sort by name or type? (name/type): ");
    var sort = Console.ReadLine();

    if (sort != "name" && sort != "type")
    {
        Console.WriteLine("Sort must be 'name' or 'type'.");
        return;
    }

    Console.Write("Include file name and relative path? (y/n): ");
    var noteInput = Console.ReadLine();
    var note = noteInput?.Trim().ToLowerInvariant() == "y";

    Console.Write("Remove empty lines? (y/n): ");
    var removeEmptyLinesInput = Console.ReadLine();
    var removeEmptyLines = removeEmptyLinesInput?.Trim().ToLowerInvariant() == "y";

    Console.Write("Enter author name (leave empty to skip): ");
    var author = Console.ReadLine();

    var rspLines = new List<string>
{
    "bundle",
    "--language",
    languages,
    "--output",
    $"\"{outputPath}\"",
    "--sort",
    sort ?? "name"
};

    if (note)
        rspLines.Add("--note");

    if (removeEmptyLines)
        rspLines.Add("--remove-empty-lines");

    if (!string.IsNullOrWhiteSpace(author))
    {
        rspLines.Add("--author");
        rspLines.Add($"\"{author}\"");
    }

    File.WriteAllLines(responseFileName, rspLines);

    Console.WriteLine($"Response file created: {Path.GetFullPath(responseFileName)}");
});

var rootCommand = new RootCommand("Root command for file bundler CLI");
rootCommand.Description = "A command-line tool for bundling source code files.";

rootCommand.Subcommands.Add(bundleCommand);
rootCommand.Subcommands.Add(createRspCommand);

return rootCommand.Parse(args).Invoke();