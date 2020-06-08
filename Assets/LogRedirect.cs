using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;

public class LogRedirect
{
    //自己封装的日志脚本函数
    private const string myLogScriptName = "LogMgr.cs";

    //封装次数
    private static int layer = 3;
    
    private static Assembly unityEditorAssembly;
    



    //反射控制日志行数，即 单击的是第几行
    private static int GetConsoleLogRow()
    {

        unityEditorAssembly = Assembly.GetAssembly(typeof(EditorWindow));

        Type consoleWindowType = unityEditorAssembly.GetType("UnityEditor.ConsoleWindow");
        FieldInfo fieldInfo = consoleWindowType.GetField("ms_ConsoleWindow", BindingFlags.Static | BindingFlags.NonPublic);
        //ms_ConsoleWindow 是静态字段
        EditorWindow consoleWindow = fieldInfo.GetValue(null) as EditorWindow;

        FieldInfo m_ListView = consoleWindowType.GetField("m_ListView", BindingFlags.Instance | BindingFlags.NonPublic);


        FieldInfo logListViewCurrentRow = m_ListView.FieldType.GetField("row", BindingFlags.Instance | BindingFlags.Public);

        object logListView = m_ListView.GetValue(consoleWindow);
        int row = (int)logListViewCurrentRow.GetValue(logListView);
        return row;
    }


    //获取第row行的堆栈信息字符串
    private static string GetLogStr(int row)
    {

        Type logEntriesType = unityEditorAssembly.GetType("UnityEditor.LogEntries");
        MethodInfo LogEntriesGetEntry = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);

        Type logEntryType = unityEditorAssembly.GetType("UnityEditor.LogEntry");
        object logEntry = Activator.CreateInstance(logEntryType);

        FieldInfo logEntryCondition = logEntryType.GetField("condition", BindingFlags.Instance | BindingFlags.Public);

        // 源码通过行数返回一个含Log信息的结构体
        // LogEntry entry = new LogEntry();
        // LogEntries.GetEntryInternal(m_ListView.row, entry);
        LogEntriesGetEntry.Invoke(null, new object[] { row, logEntry });
        return logEntryCondition.GetValue(logEntry) as string;
    }

    //解析真正调用的脚本和代码行
    private static string ParseFileName(string condition, ref int line)
    {
        //不是经过我们封装的日志 返回null
        //在函数的堆栈信息中 会包含调用者的函数名和脚本名字 所以可以根据查找字符串的方式进行简单判断
        if (condition.IndexOf(myLogScriptName) < 0)
        {
            return null;
        }

        try
        {

            // unity日志堆栈参考
            /* test
            *  UnityEngine.Debug:Log(Object, Object)
            *  LogMgr:Log(Object, Object) (at Assets/LogMgr.cs:12)
            *  TestLog:Start() (at Assets/TestLog.cs:9)
            */
            var rows = condition.Split('\n');
            string row = rows[2 + layer];
            //UnityEngine.Debug.LogError("目标内容:" + row);
            int index = row.LastIndexOf(".cs:");
            string rowNumberStr = row.Substring(index + 4, row.Length - 1 - index - 4);
            Int32.TryParse(rowNumberStr, out line);
            //UnityEngine.Debug.LogError("解析出代码行数:" + line);
            int index2 = row.LastIndexOf("/");
            string csFile = row.Substring(index2 + 1, index - index2 - 1) + ".cs";
            //UnityEngine.Debug.LogError("解析出脚本名:" + file);
            return csFile;
        }
        catch
        {
            return null;
        }

    }

    //如果需要加一些限制
    private static bool RedirectLimit(int instanceID)
    {
        return EditorWindow.focusedWindow.titleContent.text.Equals("Console");
    }


    private static int curInstanceID;
    private static int curHasOpenLine;
    [OnOpenAssetAttribute(0)]
    public static bool OnOpenAsset(int instanceID, int line)
    {
        if (!RedirectLimit(instanceID)) return false;
        //无需重复计算
        if (curInstanceID == instanceID && curHasOpenLine == line)
        {
            curInstanceID = -1;
            curHasOpenLine = -1;
            return false;
        }
        curInstanceID = instanceID;
        curHasOpenLine = line;


        string fileName = "";
        try
        {
            int row = GetConsoleLogRow();
            string logStr = GetLogStr(row);
            fileName = ParseFileName(logStr, ref line);

        }
        catch
        {
            return false;
        }
        //出现异常  或文件不是cs脚本
        if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".cs"))
        {
            return false;
        }



        //打开该文件并定位到相关行数
        string filter = fileName.Substring(0, fileName.Length - 3);
        filter += " t:MonoScript";
        string[] searchPaths = AssetDatabase.FindAssets(filter);

        for (int i = 0; i < searchPaths.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(searchPaths[i]);

            if (path.EndsWith(fileName))
            {
                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(path, typeof(MonoScript));
                AssetDatabase.OpenAsset(obj, line);
                return true;
            }
        }
        return false;
    }
}