using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace EmotionPlayer
{
    public partial class FileWindow
    {
        public IEnumerable<string> Sources
        {
            get
            {
                foreach (string source in list.Items)
                    yield return source;
            }
        }

        public void Close(bool dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }
        public new void DragMove()
        {
            list.RemoveSelection();
            base.DragMove();
        }
        public void OpenSourcesDialog()
        {
            OpenFileDialog OF = new OpenFileDialog();
            OF.Multiselect = true;
            OF.Filter = filter;

            if (OF.ShowDialog() == true)
                list.Items.AddRange(OF.FileNames.Where(a => list.Items.IndexOf(a) < 0));
        }

    }
    
}
