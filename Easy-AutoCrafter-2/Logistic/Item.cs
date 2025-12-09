using System;
using System.Collections.Generic;

namespace IngameScript
{
    public partial class Program
    {
        public class Item : IVisualItem
        {
            public Item(string fullName, Program program, int amount = 0)
            {
                if (!fullName.StartsWith(OB))
                    fullName = OB + fullName;

                var temp = fullName.Split('/');
                Sprite = fullName;
                FullName = fullName;
                Name = NaturalName = temp[1];
                Type = temp[0].Substring(CHARACTERS_TO_SKIP);
                Amount = amount;
                KeyString = program._translateEnabled ? fullName.Substring(CHARACTERS_TO_SKIP).ToLower() : "";
            }

            public string FullName;
            public string KeyString;
            public double Amount;
            public string Name;
            public string Type;
            public Blueprint Blueprint { get; set; }
            public string Sprite { get; }
            public string NaturalName { get; set; }
            public string Description => $"{MetricFormat(Amount)} Stored";
        }
    }
}