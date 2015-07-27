using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Diagnostics;

namespace Solar600
{
    public class Zon
    {
        public struct langeInfo
        {

            public string FirmwareID;
            public string FirmwareRev;
            public string FirmwareDate;
            public int MaxACPower;
        }

        public struct huidigeInfo
        {
            public long RunningHours;
            public float TotalKwh;
            public int mainsVoltage;
            public float solarVoltage;
            public float solarCurrent;
            public int ACPower;
            public int Temperature;
            public byte PowerOutput; //berekend veld door ACPower / MaxACPower
            public byte Activity; //0=maan 1=wolk 2=kleine zon 3=zon kleine wolk 4=zon;  (berekend veld van PowerOutput)
        }

        public struct HistoricalData
        {
            public DateTime dag;
            public float KWh;
            public long Duration;
        }

        private void clear()
        {
            huidige.RunningHours = 0;
            huidige.TotalKwh = 0;
            lange.FirmwareID = string.Empty;
            lange.FirmwareRev = string.Empty;
            lange.FirmwareDate = string.Empty;
            lange.MaxACPower = 0;

            historisch = new HistoricalData[10];

            for (int i = 0; i < 10; i++)
            {

                //Historisch[i] = new HistoricalData();
                historisch[i].dag = DateTime.Now.AddDays(-1 * i);
                historisch[i].KWh = 0;
                historisch[i].Duration = 0;
            }

            huidige.mainsVoltage = 0;
            huidige.solarVoltage = 0;
            huidige.solarCurrent = 0;
            huidige.ACPower = 0;
            huidige.Temperature = 0;
            huidige.PowerOutput = 0;
            huidige.Activity = 0;
        }

        string _PortNummer;
        DateTime _LaatsteOphaling;

        public DateTime LaatsteOphaling
        {
            get { return _LaatsteOphaling; }
        }

        public bool LinkStatus
        {
            get { return (port != null ? port.IsOpen : false); }
        }

        public string PortNummer
        {
            get { return _PortNummer; }
        }

        private huidigeInfo huidige;

        public huidigeInfo Huidige
        {
            get { return huidige; }
        }
        langeInfo lange;

        public langeInfo Lange
        {
            get { return lange; }
        }
        HistoricalData[] historisch;

        public HistoricalData[] Historisch
        {
            get { return historisch; }

        }

        SerialPort port;

        private DateTime laatsteLang;

        public Zon(string PortNummer)
        {
            _PortNummer = PortNummer;
            huidige = new huidigeInfo();
            lange = new langeInfo();
            clear();
            laatsteLang = DateTime.MinValue;

            try
            {
                EventLog.WriteEntry("SunInLine", handshake(), EventLogEntryType.Information);
            }
            catch { } //stel Eventlog werkt niet (bijvoorbeeld onder win98....) dan hoeft alles er niet uit te knallen
            Process();
        }


        public string PrintToConsole()
        {
            string retval = string.Empty;
            retval += string.Format("Linkstatus : {0}     \n", (port != null ? port.IsOpen : false));
            retval += string.Format("Laatste data : {0}     \n", LaatsteOphaling.ToString("dd-MM-yyyy HH:mm:ss"));
            retval += string.Format("mainsVoltage: {0} Volt     \n", huidige.mainsVoltage);
            retval += string.Format("solarVoltage: {0} Volt     \n", huidige.solarVoltage);
            retval += string.Format("solarCurrent: {0} Amp     \n", huidige.solarCurrent);
            retval += string.Format("ACPower: {0} Watt     \n", huidige.ACPower);
            retval += string.Format("Temperature: {0}     \n", huidige.Temperature);
            retval += string.Format("PowerOutput: {0} %    \n", huidige.PowerOutput);
            retval += string.Format("Activity: {0}     \n", huidige.Activity);
            retval += string.Format("MaxACPower: {0}     \n", lange.MaxACPower);
            retval += string.Format("Total: {0} Kwh     \n", huidige.TotalKwh);
            retval += string.Format("RunningHours: {0}     \n", huidige.RunningHours / 60);
            retval += string.Format("--------------------------------\n");
            retval += string.Format("Max AC Power: {0}     \n", lange.MaxACPower);
            retval += string.Format("FirmwareID: {0}     \n", lange.FirmwareID);
            retval += string.Format("FirmwareRev: {0}     \n", lange.FirmwareRev);
            retval += string.Format("FirmwareDate: {0}     \n", lange.FirmwareDate);
            retval += string.Format("--------------------------------\n");
            for (int i = 0; i < 10; i++)
            {
                DateTime d = new DateTime(2008, 1, 1);
                d = d.AddMinutes(historisch[i].Duration);
                retval += string.Format("{0}: {1} KWh  {2} Hours\n", historisch[i].dag.ToString("dd-MM"), historisch[i].KWh, d.ToString("HH:mm"));
            }
            return retval;
        }

        public void Process()
        {
            try
            {
                if (!(port != null ? port.IsOpen : false))
                {
                    handshake();
                }

                if ((port != null ? port.IsOpen : false))
                {
                    if (historisch[0].dag.ToString("dd-MM-yyyy") != DateTime.Now.ToString("dd-MM-yyyy")) clear();
                    GetHuidige();
                    if (laatsteLang.AddMinutes(5) < DateTime.Now) GetLang();
                    _LaatsteOphaling = DateTime.Now;
                }

            }
            catch { close(); };
        }

        public void close()
        {
            try
            {
                port.Close();
            }
            catch { }
        }


        private bool ontvang(out byte[] retval)
        {
            System.Threading.Thread.Sleep(100);
            retval = new byte[1];
            try
            {
                port.ReadTimeout = 100;
                int aantalBytes = port.BytesToRead;
                retval = new byte[aantalBytes];
                port.Read(retval, 0, aantalBytes);
            }
            catch { close(); }

            #region check de checksum
            //sum alle bytes samen en modulo 256 de laatste byte.
            byte checkbyte = 0;
            for (int ii=0; ii < retval.Length-1; ii++)
            {
                checkbyte += retval[ii];
            }
            #endregion

            return retval[retval.Length-1].Equals(checkbyte);
        }

        private string handshake()
        {
            string retval = string.Empty;
            try
            {
                bool isConnected = (port != null ? port.IsOpen : false);

                if (!isConnected)
                {
                    retval += string.Format("handshake ({0}): ", DateTime.Now.ToString("dd-MM HH:mm:ss"));
                    port = new SerialPort(_PortNummer, 9600, Parity.None, 8, StopBits.One); ;
                    port.Open();
                    port.RtsEnable = true;
                    port.DtrEnable = true;
                }
                if ((port != null ? port.IsOpen : false))
                {
                    byte[] b = { 0, 0, 0, 0, 193, 0, 0, 0, 193 };
                    stuur(b);
                    ontvang(out b);
                    retval += string.Format("Ok\n");
                    //Console.WriteLine(BtoS(ontvang()));
                }
                else { retval += string.Format("closed\n"); }
            }
            catch { retval += string.Format("Error\n"); close(); }
            return retval;
        }

        private string BtoS(byte[] b)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < b.Length; i++)
            {
                sb.Append(string.Format("{0} ", b[i]));
            }
            return sb.ToString();
        }

        private void stuur(byte[] b)
        {
            port.Write(b, 0, b.Length);
        }

        private void GetHuidige()
        {
            byte[] b = { 17, 0, 0, 0, 182, 0, 0, 0, 199 };
            port.Write(b, 0, b.Length);
            ontvang(out b);
            huidige.solarVoltage = ((float)(b[8] + (b[9] * 256))) / 10.0f;
            huidige.solarCurrent = ((float)(b[10] + (b[11] * 256))) / 100.0f;
            huidige.mainsVoltage = b[14];
            huidige.ACPower = b[18] + b[19] * 256;
            huidige.TotalKwh = ((float)(b[20] + (b[21] * 256) + (b[22] * 65536))) / 100.0f;
            huidige.RunningHours = ((long)(b[24] + (b[25] * 256) + (b[26] * 65536)));
            huidige.Temperature = b[23];
            if (huidige.ACPower > 0)
            {
                huidige.PowerOutput = (byte)Math.Round((((float)huidige.ACPower) / ((float)lange.MaxACPower)) * 100, 0);
            }
            if (huidige.PowerOutput > 80) huidige.Activity = 4;
            else if (huidige.PowerOutput > 60) huidige.Activity = 3;
            else if (huidige.PowerOutput > 40) huidige.Activity = 2;
            else if (huidige.PowerOutput > 20) huidige.Activity = 1; 
            else huidige.Activity = 0;

        }

        private void GetLang()
        {
            byte[] b = { 17, 0, 0, 0, 185, 0, 0, 0, 202 };
            port.Write(b, 0, b.Length);
            ontvang(out b);

            lange.MaxACPower = b[24] + (b[25] * 256);

            byte[] b2 = { 17, 00, 00, 00, 180, 00, 00, 00, 197 };
            port.Write(b2, 0, b2.Length);
            ontvang(out b2);

            lange.FirmwareID = Convert.ToString(b[13], 16);
            lange.FirmwareRev = Convert.ToString(b[16], 16) + "." + Convert.ToString(b[15], 16);
            lange.FirmwareDate = Convert.ToString(b[18], 16) + Convert.ToString(b[17], 16);

            for (int i = 0; i < 10; i++)
            {
                byte[] b3 = { 17, 0, 0, 0, 154, 0, 0, 0, 171 };
                b3[5] = Convert.ToByte(i);
                b3[8] = Convert.ToByte(171 + i);
                port.Write(b3, 0, b3.Length);
                ontvang(out b);
                historisch[i].KWh = (((float)b[6]) + ((float)b[7]) * 256) / 100.0f;
                historisch[i].Duration = b[5] * 5; //De tijd is in 5 minuten.

            }
            laatsteLang = DateTime.Now;
        }
    }
}
