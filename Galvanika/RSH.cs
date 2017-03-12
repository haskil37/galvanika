using System.Collections.Generic;
using RshCSharpWrapper;
using RshCSharpWrapper.RshDevice;
using System;
using System.Linq;

namespace Galvanika
{
    class RSH
    {
        const string BOARD_NAME = "LA48DPCI";
        RSH_API st;
        Device device = new Device(BOARD_NAME);
        RshBoardPortInfo bpi = new RshBoardPortInfo();

        public bool Connect()
        {
            st = device.OperationStatus;
            //Коннектимся к первой плате
            st = device.Connect(1);
            if (st != RSH_API.SUCCESS)
                return false;

            st = device.Get(RSH_GET.DEVICE_PORT_INFO, ref bpi);
            for (int i = 0; i < bpi.confs.Length; i++)
            {
                RshInitPort port = new RshInitPort();
                port.operationType = RshInitPort.OperationTypeBit.Write;
                port.portAddress = bpi.confs[i].address;
                port.portValue = 0x80;
                st = device.Init(port); //У первой платы все на вывод
            }

            //Сбрасываем все порты в 0, но т.к. у нас инверсия то в ff
            RshInitPort p = new RshInitPort();
            p.operationType = RshInitPort.OperationTypeBit.Write;
            p.portAddress = 0;
            p.portValue = 0xff;
            st = device.Init(p);
            p.portAddress = 1;
            p.portValue = 0xff;
            st = device.Init(p);
            p.portAddress = 2;
            p.portValue = 0xff;
            st = device.Init(p);
            //-------------------

            //Коннектимся ко второй плате
            st = device.Connect(2);
            if (st != RSH_API.SUCCESS)
                return false;

            st = device.Get(RSH_GET.DEVICE_PORT_INFO, ref bpi);
            for (int i = 0; i < bpi.confs.Length; i++)
            {
                RshInitPort port = new RshInitPort();
                port.operationType = RshInitPort.OperationTypeBit.Write;
                port.portAddress = bpi.confs[i].address;
                port.portValue = 0x9B;
                st = device.Init(port); //У второй платы все на ввод
            }
            return true;
        }
        public List<int> Read()
        {
            st = device.Connect(2);
            List<int> InputData = new List<int>();
            RshInitPort p = new RshInitPort();
            p.operationType = RshInitPort.OperationTypeBit.Read;
            for (uint i = 0; i < 3; i++)
            {
                p.portAddress = i;
                st = device.Init(p);
                if (st != RSH_API.SUCCESS)
                    return new List<int>() { 0, 0, 0, 0 };

                var bits = Convert.ToString(p.portValue, 2);
                while (bits.Length < 8)
                    bits = bits.Insert(0, "0");
                bits = InverseString(bits);
                var byteToSave = Convert.ToByte(bits, 2);
                InputData.Add(byteToSave);
            }

            p.portAddress = 4;
            st = device.Init(p);
            if (st != RSH_API.SUCCESS)
                return new List<int>() { 0, 0, 0, 0 };

            var bits2 = Convert.ToString(p.portValue, 2);
            while (bits2.Length < 8)
                bits2 = bits2.Insert(0, "0");
            bits2 = InverseString(bits2);
            var byteToSave2 = Convert.ToByte(bits2, 2);
            InputData.Add(byteToSave2);


            return InputData;
        }
        public bool Write(List<int> outputData)
        {
            st = device.Connect(1);
            RshInitPort p = new RshInitPort();
            p.operationType = RshInitPort.OperationTypeBit.Write;
            for (uint i = 0; i < 3; i++)
            {
                var bits = Convert.ToString(outputData[Convert.ToInt32(i)], 2);
                while (bits.Length < 8)
                    bits = bits.Insert(0, "0");
                bits = InverseString(bits);
                var byteToSave = Convert.ToByte(bits, 2);
                p.portAddress = i;
                p.portValue = byteToSave;
                st = device.Init(p);
                if (st != RSH_API.SUCCESS)
                    return false;
            }
            return true;
        }
        private string InverseString(string s)
        {
            char[] value = s.ToCharArray();
            string inverseValue = "";
            foreach (var item in value)
            {
                if (item == '0')
                    inverseValue += "1";
                else
                    inverseValue += "0";
            }
            return inverseValue;
        }
    }
}