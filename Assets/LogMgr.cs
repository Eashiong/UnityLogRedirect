using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogMgr
{
	public static void Log(object message,Object context)
	{
		_Log(message,context);

		
		
	}
	private static void _Log(object message,Object context)
	{
		__Log(message,context);
	}

	private static void __Log(object message,Object context)
	{
		Debug.Log(message,context);
	}
}
