using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentSearchRagDotNetAI.Utils
{
    public static class DocumentService
    {
        public static IEnumerable<string> GetAllTxtDocumentsFromDirectoryPath(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"O diretório especificado não foi encontrado: {directoryPath}");
            }

            var txtFiles = Directory.EnumerateFiles(directoryPath, "*.txt", SearchOption.TopDirectoryOnly);
            
            return txtFiles;
        }

    }
}
