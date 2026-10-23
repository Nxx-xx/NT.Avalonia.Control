using Avalonia.Controls;

namespace TreeDataGrid.Usage;

public partial class MainWindow : Window
{
    // For compiled display bindings, configure staged writes explicitly on the column:
    // column.SetValueAccessors<Person, string>(person => person.Name, (person, value) => person.Name = value);
    public MainWindow() => InitializeComponent();
}
