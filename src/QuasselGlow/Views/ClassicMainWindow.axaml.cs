using Avalonia.Controls;

namespace QuasselGlow.Views;

public partial class ClassicMainWindow : MainWindowBase
{
    public ClassicMainWindow()
    {
        InitializeComponent();
        InitializeWindow();
    }

    protected override ListBox ActiveChatListBox => ChatMessagesListBox;

    protected override Canvas MaximizeGlyphCanvas => MaximizeIconCanvas;

    protected override Canvas RestoreGlyphCanvas => RestoreIconCanvas;

    protected override TextBox? ResolveComposerTextBox()
    {
        return MainComposerTextBox.IsVisible ? MainComposerTextBox : null;
    }
}
