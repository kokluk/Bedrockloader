namespace KoklukBedrockModloader;

internal sealed class DragTextBox : TextBox
{
    Point? dragStart;
    int originalStart, originalLength;

    public DragTextBox() => AllowDrop = true;

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0201 && SelectionLength > 0 && !UseSystemPasswordChar && PasswordChar == '\0')
        {
            var point = PointToClient(Cursor.Position);
            int index = GetCharIndexFromPosition(point);
            if (index >= SelectionStart && index < SelectionStart + SelectionLength)
            {
                originalStart = SelectionStart;
                originalLength = SelectionLength;
                dragStart = point;
                Capture = true;
                return;
            }
        }
        base.WndProc(ref message);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragStart is not { } start || e.Button != MouseButtons.Left) return;
        var threshold = SystemInformation.DragSize;
        if (Math.Abs(e.X - start.X) < threshold.Width / 2 && Math.Abs(e.Y - start.Y) < threshold.Height / 2) return;
        dragStart = null;
        Capture = false;
        var data = new DataObject(DataFormats.UnicodeText, SelectedText);
        data.SetData(typeof(DragTextBox), this);
        if (DoDragDrop(data, ReadOnly ? DragDropEffects.Copy : DragDropEffects.Copy | DragDropEffects.Move) == DragDropEffects.Move)
        {
            Select(originalStart, originalLength);
            SelectedText = "";
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (dragStart != null)
        {
            dragStart = null;
            Capture = false;
            Select(IndexAt(e.Location), 0);
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) dragStart = null; base.OnMouseCaptureChanged(e); }
    protected override void OnDragEnter(DragEventArgs e) => OnDragOver(e);

    protected override void OnDragOver(DragEventArgs e)
    {
        e.Effect = DragDropEffects.None;
        if (ReadOnly || UseSystemPasswordChar || PasswordChar != '\0' || e.Data?.GetDataPresent(DataFormats.UnicodeText) != true ||
            ReferenceEquals(e.Data.GetData(typeof(DragTextBox)), this)) return;
        e.Effect = (e.KeyState & 8) == 0 && (e.AllowedEffect & DragDropEffects.Move) != 0
            ? DragDropEffects.Move : e.AllowedEffect & DragDropEffects.Copy;
    }

    int IndexAt(Point point)
    {
        int index = GetCharIndexFromPosition(point);
        return index == TextLength - 1 && point.X >= GetPositionFromCharIndex(TextLength).X ? TextLength : index;
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        OnDragOver(e);
        if (e.Effect == DragDropEffects.None || e.Data?.GetData(DataFormats.UnicodeText) is not string text) return;
        if (!Multiline) text = text.ReplaceLineEndings(" ");
        if (text.Length > MaxLength - TextLength) { e.Effect = DragDropEffects.None; return; }
        Focus();
        Select(IndexAt(PointToClient(new Point(e.X, e.Y))), 0);
        SelectedText = text;
    }
}
