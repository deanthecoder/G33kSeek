using System.Collections.ObjectModel;
using System.Linq;
using DTC.Core.ViewModels;
using G33kSeek.Models;

namespace G33kSeek.ViewModels;

/// <summary>
/// Provides the initial launcher surface state for G33kSeek.
/// </summary>
/// <remarks>
/// This keeps the first-run experience simple while the provider system is still being built out.
/// </remarks>
public class MainWindowViewModel : ViewModelBase
{
    private static readonly LauncherResult[] SeedResults =
    [
        new("App and file search", "Start typing with no prefix to search apps or files.", "default"),
        new("Calculator mode", "Use = to evaluate calculations like =2+2.", "="),
        new("Web search", "Use ? to send a query to your browser.", "?"),
        new("Content search", "Use ?? to grep through files.", "??"),
        new("AI prompt", "Use @ to route text to an AI provider.", "@"),
        new("Commands", "Use > to execute launcher commands.", ">")
    ];

    private string m_searchText;
    private string m_statusText = "Press Ctrl+Space to toggle the launcher. Esc dismisses it.";
    private LauncherResult m_selectedResult;

    public MainWindowViewModel()
    {
        Results = new ObservableCollection<LauncherResult>(SeedResults);
        SelectedResult = Results.FirstOrDefault();
    }

    public ObservableCollection<LauncherResult> Results { get; }

    public string SearchText
    {
        get => m_searchText;
        set
        {
            if (!SetField(ref m_searchText, value))
                return;

            RefreshResults();
        }
    }

    public string StatusText
    {
        get => m_statusText;
        set => SetField(ref m_statusText, value);
    }

    public LauncherResult SelectedResult
    {
        get => m_selectedResult;
        set => SetField(ref m_selectedResult, value);
    }

    private void RefreshResults()
    {
        var query = SearchText?.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? SeedResults
            : SeedResults.Where(
                    result => result.Title.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                              result.Subtitle.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                              result.PrefixHint.Contains(query, System.StringComparison.OrdinalIgnoreCase))
                .ToArray();

        Results.Clear();
        foreach (var result in filtered)
            Results.Add(result);

        SelectedResult = Results.FirstOrDefault();
        StatusText = Results.Count > 0
            ? "Prototype launcher is listening. Query providers come next."
            : "No seeded matches yet. Providers will populate this list.";
    }
}
