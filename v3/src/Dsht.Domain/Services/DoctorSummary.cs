using System.Collections.Generic;
using Dsht.Domain.Model;

namespace Dsht.Domain.Services
{
    /// <summary>体检汇总与级别名（纯函数）。逐字对齐 v2.x 的 DoctorSummary / DocLevel：
    ///   DOCTOR_OK 0 / DOCTOR_WARN n / DOCTOR_ERROR n（ERROR 优先）。</summary>
    public static class DoctorSummary
    {
        public static string Summary(List<DocItem> items)
        {
            int err = 0, warn = 0;
            if (items != null)
            {
                foreach (DocItem it in items)
                {
                    if (it.Level == 2) err++;
                    else if (it.Level == 1) warn++;
                }
            }
            if (err > 0) return "DOCTOR_ERROR " + err;
            if (warn > 0) return "DOCTOR_WARN " + warn;
            return "DOCTOR_OK 0";
        }

        public static string Level(int level)
        {
            return level == 2 ? "ERROR" : (level == 1 ? "WARN" : "OK");
        }
    }
}