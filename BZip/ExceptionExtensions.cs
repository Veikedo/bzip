using System;

namespace BZip
{
  public static class ExceptionExtensions
  {
    public static bool HandleInner(this Exception exception, Func<Exception, bool> action)
    {
      Exception? next = exception;
      while (next != null)
      {
        if (action(next))
        {
          return true;
        }

        next = next.InnerException;
      }

      return false;
    }
  }
}