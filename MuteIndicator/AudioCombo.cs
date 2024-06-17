using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace MuteIndicator
{
    public class AudioComboCollection : Collection<AudioCombo>
    {
        public delegate void CollectionChangedEventHandler(object sender, NotifyCollectionChangedEventArgs e);
        public event CollectionChangedEventHandler? CollectionChanged;

        protected override void InsertItem(int index, AudioCombo item)
        {
            base.InsertItem(index, item);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        protected override void RemoveItem(int index)
        {
            AudioCombo item = this[index];
            base.RemoveItem(index);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private const string Separator = "\n";

        public static AudioComboCollection CreateFromString(string s)
        {
            var c = new AudioComboCollection();
            var strings = s.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var s1 in strings)
                c.Add(AudioCombo.CreateFromString(s1) ?? throw new InvalidOperationException());

            return c;
        }

        public static string ConvertToString(AudioComboCollection c)
        {
            return string.Join(Separator, c.Select(AudioCombo.ConvertToString));
        }
    }

    public class AudioCombo
    {
        private bool Equals(AudioCombo other)
        {
            return InputDeviceName == other.InputDeviceName && OutputDeviceName == other.OutputDeviceName;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            return Equals((AudioCombo) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(InputDeviceName, OutputDeviceName);
        }

        public string InputDeviceName { get; }
        public string OutputDeviceName { get; }

        public AudioCombo(string inputDeviceName, string outputDeviceName)
        {
            InputDeviceName = inputDeviceName;
            OutputDeviceName = outputDeviceName;
        }

        public string DisplayText => $"{InputDeviceName} - {OutputDeviceName}";

        private const string Separator = "<>";
        public static AudioCombo? CreateFromString(string s)
        {
            var names = (s ?? "").Split(Separator.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (names.Length != 2) return null;
            return new AudioCombo(names[0], names[1]);
        }

        public static string ConvertToString(AudioCombo c)
        {
            return $"{c.InputDeviceName}{Separator}{c.OutputDeviceName}";
        }
    }
}
