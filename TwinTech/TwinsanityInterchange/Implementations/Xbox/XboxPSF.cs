using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox
{
    /// <summary>
    /// Font of the Xbox version, its characters store the box they take on their page as fractions of the page
    /// </summary>
    public class XboxPSF : BaseTwinItem, ITwinPSF
    {
        private const Int32 CharacterLength = 28;

        // What the game had for every character, written again while the character wasn't changed
        private List<Byte[]> storedCharacters = new();

        public List<ITwinPTC> FontPages { get; set; }
        public List<VectorCharacterData> CharacterData { get; set; }
        public Int32 SpaceIdentifier { get; set; }

        public XboxPSF()
        {
            FontPages = new List<ITwinPTC>();
            CharacterData = new List<VectorCharacterData>();
        }

        public override Int32 GetLength()
        {
            return 4 + FontPages.Sum(f => f.GetLength()) + 8 + CharacterData.Count * CharacterLength;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            var pages = reader.ReadInt32();
            for (var i = 0; i < pages; ++i)
            {
                var page = new XboxPTC();
                page.Read(reader, 0);
                FontPages.Add(page);
            }

            var characters = reader.ReadInt32();
            SpaceIdentifier = reader.ReadInt32();
            storedCharacters = new List<Byte[]>(characters);
            for (var i = 0; i < characters; ++i)
            {
                var stored = reader.ReadBytes(CharacterLength);
                storedCharacters.Add(stored);
                CharacterData.Add(ToCharacter(stored));
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(FontPages.Count);
            foreach (var page in FontPages)
            {
                page.Write(writer);
            }

            writer.Write(CharacterData.Count);
            writer.Write(SpaceIdentifier);
            for (var i = 0; i < CharacterData.Count; ++i)
            {
                var character = CharacterData[i];
                if (i < storedCharacters.Count && IsSame(ToCharacter(storedCharacters[i]), character))
                {
                    writer.Write(storedCharacters[i]);
                    continue;
                }

                var (width, height) = GetPageSize(character.FontPageSpecifier);
                writer.Write(character.Size.Y);
                writer.Write(character.Size.X);
                writer.Write(character.PageUv.X / width);
                writer.Write(character.PageUv.Y / height);
                writer.Write((character.PageUv.X + character.Size.X) / width);
                writer.Write((character.PageUv.Y - character.Size.Y) / height);
                writer.Write((Int32)character.FontPageSpecifier);
            }
        }

        // The box starts at the bottom left of the character and goes up by its height
        private VectorCharacterData ToCharacter(Byte[] stored)
        {
            var page = BitConverter.ToInt32(stored, 24);
            var (width, height) = GetPageSize((Byte)page);
            return new VectorCharacterData
            {
                Size = new Vector2 { X = BitConverter.ToSingle(stored, 4), Y = BitConverter.ToSingle(stored, 0) },
                PageUv = new Vector2 { X = BitConverter.ToSingle(stored, 8) * width, Y = BitConverter.ToSingle(stored, 12) * height },
                FontPageSpecifier = (Byte)page
            };
        }

        private (Single Width, Single Height) GetPageSize(Byte page)
        {
            var texture = page < FontPages.Count ? FontPages[page].Texture : FontPages.FirstOrDefault()?.Texture;
            return texture == null ? (256, 256) : (1 << texture.ImageWidthPower, 1 << texture.ImageHeightPower);
        }

        private static Boolean IsSame(VectorCharacterData first, VectorCharacterData second)
        {
            return first.FontPageSpecifier == second.FontPageSpecifier && first.Size.X == second.Size.X && first.Size.Y == second.Size.Y &&
                   first.PageUv.X == second.PageUv.X && first.PageUv.Y == second.PageUv.Y;
        }
    }
}
