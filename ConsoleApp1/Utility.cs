using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GhibliPlanner
{
    public static class Utility
    {
        public static List<MovieFile> ConvertFromJson(string json)
        {
            List<MovieFile> movies = JsonConvert.DeserializeObject<List<MovieFile>>(json);
            return movies;
        }
    }
}
