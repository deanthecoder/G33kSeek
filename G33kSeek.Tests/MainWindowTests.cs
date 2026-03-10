// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Controls;
using G33kSeek.Models;
using G33kSeek.Views;

namespace G33kSeek.Tests;

public class MainWindowTests
{
    [Test]
    public void GetNextSelectedIndexReturnsMinusOneWhenThereAreNoItems()
    {
        var nextIndex = MainWindow.GetNextSelectedIndex(-1, 0, 1);

        Assert.That(nextIndex, Is.EqualTo(-1));
    }

    [Test]
    public void GetNextSelectedIndexSelectsFirstItemWhenMovingDownWithoutSelection()
    {
        var nextIndex = MainWindow.GetNextSelectedIndex(-1, 4, 1);

        Assert.That(nextIndex, Is.EqualTo(0));
    }

    [Test]
    public void GetNextSelectedIndexSelectsLastItemWhenMovingUpWithoutSelection()
    {
        var nextIndex = MainWindow.GetNextSelectedIndex(-1, 4, -1);

        Assert.That(nextIndex, Is.EqualTo(3));
    }

    [Test]
    public void GetNextSelectedIndexClampsAtBounds()
    {
        Assert.That(MainWindow.GetNextSelectedIndex(0, 4, -1), Is.EqualTo(0));
        Assert.That(MainWindow.GetNextSelectedIndex(3, 4, 1), Is.EqualTo(3));
    }

    [Test]
    public void TryGetResultFromSourceFindsResultOnAncestor()
    {
        var result = new QueryResult("image.png");
        var ancestor = new Border
        {
            DataContext = result
        };
        var child = new TextBlock();
        ancestor.Child = child;

        var resolvedResult = MainWindow.TryGetResultFromSource(child);

        Assert.That(resolvedResult, Is.SameAs(result));
    }

    [Test]
    public void TryGetResultFromSourceReturnsNullWhenNoAncestorHasResult()
    {
        var control = new TextBlock();

        var resolvedResult = MainWindow.TryGetResultFromSource(control);

        Assert.That(resolvedResult, Is.Null);
    }
}
