using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MDCSharpNetLib.Utility;
namespace MDCSharpNetLib.Network
{
    public class Packet
    {
        public Session Owner { get; private set; }
        public byte[] Buffer { get; private set; }
        public int Position { get; private set; }
        public int Size { get; private set; }

        public Int16 ProtocolId { get; private set; }

        public static Packet Create(Int16 protocol_id)
        {
            Packet packet = new Packet();
            packet.SetProtocol(protocol_id);
            return packet;
        }

        public Packet(ArraySegment<byte> buffer, Session owner)
        {
            Buffer = buffer.Array;

            Position = Defines.HEADERSIZE;
            Size = buffer.Count;

            ProtocolId = PopProtocolId();
            Position = Defines.HEADERSIZE;

            Owner = owner;
        }

        public Packet(byte[] buffer, Session owner)
        {

            Buffer = buffer;

            Position = Defines.HEADERSIZE;

            Owner = owner;
        }

        public Packet()
        {
            this.Buffer = new byte[1024];
        }

        public Int16 PopProtocolId()
        {
            return PopInt16();
        }

        public void CopyTo(Packet target)
        {
            target.SetProtocol(this.ProtocolId);
            target.OverWrite(this.Buffer, this.Position);
        }

        public void OverWrite(byte[] source, int position)
        {
            Array.Copy(source, this.Buffer, source.Length);
            this.Position = position;
        }

        public byte PopByte()
        {
            byte data = this.Buffer[this.Position];
            this.Position += sizeof(byte);
            return data;
        }

        public Int16 PopInt16()
        {
            Int16 data = BitConverter.ToInt16(this.Buffer, this.Position);
            this.Position += sizeof(Int16);
            return data;
        }

        public Int32 PopInt32()
        {
            Int32 data = BitConverter.ToInt32(this.Buffer, this.Position);
            this.Position += sizeof(Int32);
            return data;
        }

        public string PopString()
        {
            // 문자열 길이는 최대 2바이트 까지. 0 ~ 32767
            Int16 len = BitConverter.ToInt16(this.Buffer, this.Position);
            this.Position += sizeof(Int16);

            // 인코딩은 utf8로 통일한다.
            string data = System.Text.Encoding.UTF8.GetString(this.Buffer, this.Position, len);
            this.Position += len;

            return data;
        }

        public float PopFloat()
        {
            float data = BitConverter.ToSingle(this.Buffer, this.Position);
            this.Position += sizeof(float);
            return data;
        }



        public void SetProtocol(Int16 protocol_id)
        {
            ProtocolId = protocol_id;
            //this.buffer = new byte[1024];

            // 헤더는 나중에 넣을것이므로 데이터 부터 넣을 수 있도록 위치를 점프시켜놓는다.
            Position = Defines.HEADERSIZE;

            PushInt16(protocol_id);
        }

        public void RecordSize()
        {
            // header + body 를 합한 사이즈를 입력한다.
            byte[] header = BitConverter.GetBytes(this.Position);
            header.CopyTo(this.Buffer, 0);
        }

        public void PushInt16(Int16 data)
        {
            byte[] temp_buffer = BitConverter.GetBytes(data);
            temp_buffer.CopyTo(this.Buffer, this.Position);
            Position += temp_buffer.Length;
        }

        public void Push(byte data)
        {
            byte[] temp_buffer = BitConverter.GetBytes(data);
            temp_buffer.CopyTo(this.Buffer, this.Position);
            Position += sizeof(byte);
        }

        public void Push(Int16 data)
        {
            byte[] temp_buffer = BitConverter.GetBytes(data);
            temp_buffer.CopyTo(this.Buffer, this.Position);
            Position += temp_buffer.Length;
        }

        public void Push(Int32 data)
        {
            byte[] temp_buffer = BitConverter.GetBytes(data);
            temp_buffer.CopyTo(this.Buffer, this.Position);
            Position += temp_buffer.Length;
        }

        public void Push(string data)
        {
            byte[] temp_buffer = Encoding.UTF8.GetBytes(data);

            Int16 len = (Int16)temp_buffer.Length;
            byte[] len_buffer = BitConverter.GetBytes(len);
            len_buffer.CopyTo(this.Buffer, this.Position);
            Position += sizeof(Int16);

            temp_buffer.CopyTo(this.Buffer, this.Position);
            Position += temp_buffer.Length;
        }

        public void Push(float data)
        {
            byte[] temp_buffer = BitConverter.GetBytes(data);
            temp_buffer.CopyTo(this.Buffer, this.Position);
            Position += temp_buffer.Length;
        }
    }
}
