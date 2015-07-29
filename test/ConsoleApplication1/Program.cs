using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using Solar600;
using System.Configuration;

namespace ConsoleApplication1
{
    class Program
    {

        static void Main(string[] args)
        {
            Zon z = new Zon(ConfigurationManager.AppSettings["com"]);
            z.Process();

            Console.Write(z.PrintToConsole());
            string filename = string.Format(ConfigurationManager.AppSettings["currentfile"], DateTime.Now);
            if (!System.IO.File.Exists(filename))
            {
                System.IO.File.AppendAllText(filename, "date;ACPower;Activity;mainsVolt;PowerOutput;Running;solarCurrent;solarVolt;Temp;TotalKwh;MaxACPower;FirmwareId;FirmwareRev,FirmwareDate;LinkStatus");
            }



            System.IO.File.AppendAllText(filename,
                String.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10};{11};{12};{13};{14}\r\n", DateTime.Now, z.Huidige.ACPower, z.Huidige.Activity, z.Huidige.mainsVoltage, z.Huidige.PowerOutput, z.Huidige.RunningHours, z.Huidige.solarCurrent, z.Huidige.solarVoltage, z.Huidige.Temperature, z.Huidige.TotalKwh,  z.Lange.MaxACPower, z.Lange.MaxACPower, z.Lange.FirmwareID, z.Lange.FirmwareRev, z.Lange.FirmwareDate, z.LinkStatus));

            
            foreach (Zon.HistoricalData hd in z.Historisch)
            {
                System.IO.File.WriteAllText(string.Format(ConfigurationManager.AppSettings["historicfile"], DateTime.Now),
                    String.Format("{0};{1};{2}\r\n", hd.dag, hd.Duration, hd.KWh));
            }

            z.close();
        }
    }
}
