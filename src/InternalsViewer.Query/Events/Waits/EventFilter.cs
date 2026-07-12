using System;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.Query.Events.Waits;

internal class EventFilter
{
    public static bool CanIgnore(string waitType)
    {
        if (waitType.StartsWith("SLEEP_"))
        {
            return true;
        }

        if (waitType.StartsWith("BROKER_"))
        {
            return true;
        }

        if (waitType.StartsWith("XE_"))
        {
            return true;
        }

        if (waitType.StartsWith("QDS_"))
        {
            return true;
        }

        if (waitType.StartsWith("FT_"))
        {
            return true;
        }

        if (waitType.StartsWith("DBMIRROR_"))
        {
            return true;
        }

        if (waitType.StartsWith("PREEMPTIVE_XE_"))
        {
            return true;
        }

        return waitType switch
        {
            "LAZYWRITER_SLEEP" => true,
            "CHECKPOINT_QUEUE" => true,
            "DIRTY_PAGE_POLL" => true,
            "REQUEST_FOR_DEADLOCK_SEARCH" => true,
            "LOGMGR_QUEUE" => true,
            "SP_SERVER_DIAGNOSTICS_SLEEP" => true,
            "ONDEMAND_TASK_QUEUE" => true,

            _ => false
        };
    }

}
