using System;
using System.Text;

namespace FollowerTalk;

internal sealed class DialogueDraft
{
    public const int Limit = 320;
    public string Text { get; private set; } = "";
    public int Caret { get; private set; }
    public int Anchor { get; private set; }
    public bool HasSelection => Caret != Anchor;
    private int Start => Math.Min(Caret, Anchor);
    private int End => Math.Max(Caret, Anchor);

    public void Clear()
    {
        Text = "";
        Caret = Anchor = 0;
    }

    public void SelectAll()
    {
        Anchor = 0;
        Caret = Text.Length;
    }

    public string SelectedText() => HasSelection ? Text.Substring(Start, End - Start) : "";

    public void Move(int position, bool select)
    {
        Caret = Math.Max(0, Math.Min(position, Text.Length));
        if (!select) Anchor = Caret;
    }

    public void MoveLeft(bool select) => Move(!select && HasSelection ? Start : Caret - 1, select);
    public void MoveRight(bool select) => Move(!select && HasSelection ? End : Caret + 1, select);

    public void DeleteBack()
    {
        if (HasSelection) DeleteSelection();
        else if (Caret > 0)
        {
            Text = Text.Remove(Caret - 1, 1);
            Caret = Anchor = Caret - 1;
        }
    }

    public void DeleteForward()
    {
        if (HasSelection) DeleteSelection();
        else if (Caret < Text.Length) Text = Text.Remove(Caret, 1);
    }

    private void DeleteSelection()
    {
        Text = Text.Remove(Start, End - Start);
        Caret = Anchor = Start;
    }

    public void Insert(string value)
    {
        if (HasSelection) DeleteSelection();
        var clean = new StringBuilder();
        foreach (char c in value)
        {
            if (clean.Length >= Limit - Text.Length) break;
            if (c == '\r' || c == '\n') clean.Append(' ');
            else if (!char.IsControl(c)) clean.Append(c);
        }
        Text = Text.Insert(Caret, clean.ToString());
        Caret = Anchor = Caret + clean.Length;
    }

    public string Display(string hint)
    {
        if (Text.Length == 0) return hint + "▌";
        var result = new StringBuilder();
        for (int i = 0; i <= Text.Length; i++)
        {
            if (i == Start && HasSelection) result.Append("<mark=#8D6B3477>");
            if (i == Caret) result.Append("<color=#FFE3A2>▌</color>");
            if (i == End && HasSelection) result.Append("</mark>");
            if (i == Text.Length) break;
            result.Append(Text[i] switch
            {
                '<' => "&lt;",
                '>' => "&gt;",
                '&' => "&amp;",
                _ => Text[i].ToString()
            });
        }
        return result.ToString();
    }
}
