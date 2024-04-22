using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestWpfApp
{
    public interface IConsoleViewModel
    {
        void Clear();
        void AppendError(string line);
    }
}
