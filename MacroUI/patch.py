import sys

xaml_file = r'C:\Users\carla\Desktop\AHK\Aula f70 macro\MacroUI\ConfigWindow.xaml'
with open(xaml_file, 'r', encoding='utf-8') as f:
    content = f.read()

# Replace Click events with Command bindings
content = content.replace('Click="AddCategory_Click"', 'Command="{Binding DataContext.TreeVM.AddCategoryCommand, RelativeSource={RelativeSource AncestorType=Window}}"')
content = content.replace('Click="AddMacro_Click"', 'Command="{Binding DataContext.TreeVM.AddMacroCommand, RelativeSource={RelativeSource AncestorType=Window}}"')
content = content.replace('Click="Delete_Click"', 'Command="{Binding DataContext.TreeVM.DeleteNodeCommand, RelativeSource={RelativeSource AncestorType=Window}}"')

content = content.replace('Click="Undo_Click"', 'Command="{Binding TreeVM.UndoCommand}"')
content = content.replace('Click="SaveAll_Click"', 'Command="{Binding SaveAllCommand}"')
content = content.replace('Click="AddHotstring_Click"', 'Command="{Binding AddHotstringCommand}"')
content = content.replace('Click="DeleteHotstring_Click"', 'Command="{Binding DataContext.DeleteHotstringCommand, RelativeSource={RelativeSource AncestorType=Window}}" CommandParameter="{Binding}"')

# Logo
content = content.replace('Click="BrowseCenterLogo_Click"', 'Command="{Binding SettingsVM.SelectCenterImageCommand}"')
content = content.replace('Click="ClearCenterLogo_Click"', 'Command="{Binding SettingsVM.ClearCenterImageCommand}"')

with open(xaml_file, 'w', encoding='utf-8') as f:
    f.write(content)
print('XAML patched')
