using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Implementations.Base
{
    public class BaseTwinSection : BaseTwinItem, ITwinSection
    {
        protected List<ITwinItem> Items { get; }
        protected Dictionary<UInt32, Type> idToClassDictionary = new Dictionary<uint, Type>();
        protected Type defaultType = typeof(BaseTwinItem);
        protected bool skip;
        protected Byte[] extraData;

        public BaseTwinSection()
        {
            Items = new List<ITwinItem>();
            extraData = Array.Empty<Byte>();
            skip = false;
        }

        public void AddItem(ITwinItem item)
        {
            Items.Add(item);
        }

        public Int32 GetItemsAmount()
        {
            return Items.Count;
        }

        public ITwinItem GetItem(Int32 index)
        {
            if (index >= Items.Count || index < 0) return null;
            return Items[index];
        }

        public T GetItem<T>(UInt32 id) where T : ITwinItem
        {
            return (T)Items.Where(item => item.GetID() == id).FirstOrDefault();
        }

        public void ChangeItemPosition(UInt32 id, Int32 position)
        {
            var item = Items.Where(item => item.GetID() == id).First();
            Items.Remove(item);
            Items.Insert(position, item);
        }

        public bool ContainsItem(UInt32 id)
        {
            return (from item in Items
                    where item.GetID() == id
                    select item).Any();
        }

        public override int GetLength()
        {
            if (skip)
            {
                return 0;
            }
            else
            {
                return 12 + Items.Count * 12 + GetContentLength() + extraData.Length;
            }
        }

        public int GetContentLength()
        {
            Int32 length = 0;
            foreach (ITwinItem item in Items)
            {
                length += item.GetLength();
            }
            return length;
        }

        protected virtual UInt32 ProcessId(UInt32 id)
        {
            return id;
        }

        public override void Read(BinaryReader reader, int length)
        {
            if (length > 0)
            {
                ComputeHash(reader.BaseStream);
                Int64 baseOffset = reader.BaseStream.Position;
                var magicNumber = reader.ReadUInt32();
                if ((magicNumber >> 0x10) >= 2 || (magicNumber & 0xFFFF) != GetMagicNumber())
                {
                    throw new Exception("Invalid section!");
                }
                UInt32 itemsCount = reader.ReadUInt32();
                UInt32 streamLength = reader.ReadUInt32();
                Record[] records = new Record[itemsCount];
                for (int i = 0; i < itemsCount; ++i)
                {
                    Record record = new Record();
                    record.Read(reader, 12);
                    records[i] = record;
                }
                Items.Clear();
                for (int i = 0; i < itemsCount; ++i)
                {
                    ITwinItem item;
                    UInt32 mapperId = ProcessId(records[i].ItemId);
                    if (idToClassDictionary.ContainsKey(mapperId))
                    {
                        Type type = idToClassDictionary[mapperId];
                        item = (ITwinItem)Activator.CreateInstance(type);
                    }
                    else
                    {
                        item = (ITwinItem)Activator.CreateInstance(defaultType);
                    }
                    reader.BaseStream.Position = records[i].Offset + baseOffset;
                    item.SetID(records[i].ItemId);
                    item.ComputeHash(reader.BaseStream, records[i].Size);
                    item.Read(reader, (Int32)records[i].Size);
                    Items.Add(item);
                }
                extraData = reader.ReadBytes((Int32)(length - (reader.BaseStream.Position - baseOffset)));
            }
            else
            {
                skip = true;
            }
        }

        public void RemoveItem<T>(uint id) where T : ITwinItem
        {
            ITwinItem listItem = GetItem<T>(id);
            if (listItem != null)
            {
                Items.Remove(listItem);
            }
        }

        protected virtual void PreprocessWrite()
        {

        }

        protected virtual UInt32 GetMagicNumber()
        {
            return 0x1;
        }

        public void SortItems(Comparison<ITwinItem> comparer)
        {
            Items.Sort(comparer);
        }

        protected void PrepareWrite()
        {
            foreach (var item in Items.Where(item => item is BaseTwinSection).Cast<BaseTwinSection>())
            {
                item.PrepareWrite();
                item.PreprocessWrite();
            }
        }

        public override void Write(BinaryWriter writer)
        {
            if (skip)
            {
                return;
            }

            writer.Write(GetMagicNumber() | (0x1 << 0x10));
            writer.Write(Items.Count);
            writer.Write(GetContentLength());
            Record record = new Record();
            record.Offset = (UInt32)(12 + Items.Count * 12);
            foreach (ITwinItem item in Items)
            {
                record.Size = (UInt32)item.GetLength();
                record.ItemId = item.GetID();
                record.Write(writer);
                record.Offset += record.Size;
            }
            foreach (ITwinItem item in Items)
            {
                item.Write(writer);
            }
            writer.Write(extraData);
        }

        public Type IdToClass(uint id)
        {
            return idToClassDictionary[id];
        }

        public override String GetName()
        {
            return $"Section {id}";
        }
    }
}
