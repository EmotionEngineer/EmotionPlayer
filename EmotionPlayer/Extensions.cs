using System.Collections;
using System.Windows.Controls;

namespace EmotionPlayer
{
    public static class ItemCollectionExtension
    {
        public static void AddRange(this ItemCollection collection, IEnumerable range)
        {
            foreach (object obj in range)
                collection.Add(obj);
        }
        public static void InsertRange(this ItemCollection collection, int index, IEnumerable range)
        {
            int x = 0;
            foreach (object obj in range)
                collection.Insert(index + x++, obj);
        }
        public static void Move(this ItemCollection collection, int index, int newIndex)
        {
            object item = collection[index];
            collection.RemoveAt(index);
            collection.Insert(newIndex, item);
        }
        public static void TryRemoveAt(this ItemCollection collection, int index)
        {
            if (index >= 0 && index < collection.Count)
                collection.RemoveAt(index);
        }
    }

    public static class ListBoxExtension
    {
        public static void RemoveSelection(this ListBox list)
        {
            list.SelectedIndex = -1;
        }
        public static void TryMoveSelected(this ListBox list, int newIndex)
        {
            if (list.SelectedIndex < 0)
                return;

            if (newIndex < 0 || newIndex >= list.Items.Count)
                return;

            list.Items.Move(list.SelectedIndex, newIndex);
            list.SelectedIndex = newIndex;
        }
    }
}
