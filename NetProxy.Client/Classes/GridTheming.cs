using NTDLS.WinFormsHelpers;

namespace NetProxy.Client.Classes
{
    /// <summary>
    /// Windows Forms dark mode does not restyle the headers of a DataGridView, which stay white. This gives them dark colors
    /// when the application is in dark mode.
    /// </summary>
    public static class GridTheming
    {
        public static void Apply(Control root)
        {
            if (Theming.IsDarkMode == false)
            {
                return;
            }

            if (root is DataGridView grid)
            {
                var headerBack = Color.FromArgb(0x3A, 0x3A, 0x3A);

                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = headerBack;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBack;
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
                grid.RowHeadersDefaultCellStyle.BackColor = headerBack;
                grid.RowHeadersDefaultCellStyle.ForeColor = Color.White;
                grid.RowHeadersDefaultCellStyle.SelectionBackColor = headerBack;
                grid.GridColor = Color.FromArgb(0x55, 0x55, 0x55);
            }

            foreach (Control child in root.Controls)
            {
                Apply(child);
            }
        }
    }
}
