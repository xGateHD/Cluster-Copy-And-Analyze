using System.Data;
using ClusterAnalyzer.SystemTree;

namespace ClusterAnalyzer;

public static class Utils
{
    internal static DataTable ToDataTable(this FileSystemNode originNode)
    {
        try
        {
            DataTable table = new();
            DataColumn nameOfObject = new("NameOfObject", typeof(string));
            DataColumn currentClusterColumn = new("CurrentCluster", typeof(uint));
            DataColumn hexNextClusterInChainColumn = new("HexNextClusterInChain", typeof(string));
            DataColumn nextClusterColumn = new("NextCluster", typeof(string));

            table.Columns.Add(nameOfObject);
            table.Columns.Add(currentClusterColumn);
            table.Columns.Add(hexNextClusterInChainColumn);
            table.Columns.Add(nextClusterColumn);

            Stack<FileSystemNode> stack = new();
            stack.Push(originNode);

            while (stack.Count > 0)
            {
                FileSystemNode current = stack.Pop();

                if (current.ClusterChain != null && current.ClusterChain.Count > 0)
                {
                    for (int i = 0; i < current.ClusterChain.Count; i++)
                    {
                        Fat32Entry entry = current.ClusterChain[i];
                        table.Rows.Add(
                            current.FullPath, // NameOfObject
                            (i == 0) ? current.FirstCluster : current.ClusterChain[i - 1].Value, // CurrentCluster
                            entry.ToString16(), // HexNextClusterInChain
                            entry.ToString() // NextCluster
                        );
                    }
                }
                else
                {
                    table.Rows.Add(current.Name, "", "", "");
                }

                if (current is DirectoryNode dirNode && dirNode.Children != null)
                {
                    for (int i = dirNode.Children.Count - 1; i >= 0; i--)
                    {
                        stack.Push(dirNode.Children[i]);
                    }
                }
            }

            return table;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FormatToDT] Ошибка: {ex.Message}\n{ex.StackTrace}");
            throw new DataException(
                $"Ошибка конвертации FileSystemNode в DataTable. Детали: {ex.Message}\n{ex.StackTrace}",
                ex
            );
        }
    }
}
