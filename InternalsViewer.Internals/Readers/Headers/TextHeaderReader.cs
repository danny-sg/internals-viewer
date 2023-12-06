﻿using System;

namespace InternalsViewer.Internals.Readers.Headers;

/// <summary>
/// Reads a header from a text chunk (DBCC PAGE output)
/// </summary>
public class TextHeaderReader(string headerText) : HeaderReader
{
    private string HeaderText { get; } = headerText;

    protected override string GetValue(string key)
    {
        var searchText = $"{key} = ";
        var keyStartPosition = HeaderText.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) + searchText.Length;
        var keyEndPosition = HeaderText.IndexOfAny(new [] {' ', '\r', '\n'}, keyStartPosition);

        return HeaderText.Substring(keyStartPosition, keyEndPosition - keyStartPosition);
    }
}