namespace NetProxy.Client.Classes
{
    internal static class UIUtility
    {
        /// <summary>
        /// Adds an item to the menu. The tag is the language-independent key which is used to identify the item when it is clicked.
        /// </summary>
        public static ToolStripItem AddMenuItem(ContextMenuStrip menu, string key, string text)
        {
            var item = menu.Items.Add(text);
            item.Tag = key;
            return item;
        }
    }
}
