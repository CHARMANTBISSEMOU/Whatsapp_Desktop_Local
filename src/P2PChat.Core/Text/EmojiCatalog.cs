using System;
using System.Collections.Generic;

namespace P2PChat.Core.Text
{
    /// <summary>Catalogue d'emojis par catégories (affichables par Segoe UI Emoji).</summary>
    public static class EmojiCatalog
    {
        public static readonly string[] Categories = new string[]
        {
            "Smileys",
            "Gestes",
            "Cœurs",
            "Animaux",
            "Nourriture",
            "Objets",
            "Symboles",
            "Drapeaux"
        };

        private static readonly string[] Smileys = new string[]
        {
            "😀", "😃", "😄", "😁", "😆", "😅", "🤣", "😂", "🙂", "🙃",
            "😉", "😊", "😇", "🥰", "😍", "🤩", "😘", "😗", "😚", "😙",
            "😋", "😛", "😜", "🤪", "🤨", "🧐", "🤓", "😎", "🥳", "😏",
            "😒", "😞", "😔", "😟", "😕", "🙁", "😣", "😖", "😫", "😩",
            "🥺", "😢", "😭", "😤", "😠", "😡", "🤬", "🤯", "😳", "🥵",
            "🥶", "😱", "😨", "😰", "🤗", "🤔", "🤭", "🤫", "🤥", "😶",
            "😐", "😑", "😬", "🙄", "😯", "😴", "🤤", "😪", "😵", "🤐",
            "🥴", "🤢", "🤮", "🤧", "😷", "🤒", "🤕", "🤑", "🤠", "😈",
            "👻", "💀", "☠️", "💩", "🤡", "👽", "🤖", "🎃"
        };

        private static readonly string[] Gestes = new string[]
        {
            "👋", "🤚", "🖐️", "✋", "🖖", "👌", "🤌", "🤏", "✌️", "🤞",
            "🤟", "🤘", "🤙", "👈", "👉", "👆", "👇", "☝️", "👍", "👎",
            "✊", "👊", "🤛", "🤜", "👏", "🙌", "👐", "🤲", "🤝", "🙏",
            "💪", "🦾", "✍️", "💅", "🤳", "👩‍✈️", "🧘", "🏃", "🚶", "💃",
            "🕺", "🤸", "🏋️", "🤼", "🤽", "🤾", "🤺", "⛹️", "🚴", "🚵"
        };

        private static readonly string[] Coeurs = new string[]
        {
            "❤️", "🧡", "💛", "💚", "💙", "💜", "🖤", "🤍", "🤎", "💔",
            "❣️", "💕", "💞", "💓", "💗", "💖", "💘", "💝", "💟", "💐",
            "♥️", "💌", "😻", "😽", "🫶", "❤️‍🔥", "❤️‍🩹", "💛", "💚", "💙"
        };

        private static readonly string[] Animaux = new string[]
        {
            "🐶", "🐱", "🐭", "🐹", "🐰", "🦊", "🐻", "🐼", "🐨", "🐯",
            "🦁", "🐮", "🐷", "🐸", "🐵", "🙈", "🙉", "🙊", "🐔", "🐧",
            "🐦", "🐤", "🦆", "🦅", "🦉", "🦇", "🐺", "🐗", "🐴", "🦄",
            "🐝", "🐛", "🦋", "🐌", "🐞", "🐜", "🕷️", "🦂", "🐢", "🐍",
            "🦎", "🐙", "🦑", "🦐", "🦀", "🐡", "🐠", "🐟", "🐬", "🐳"
        };

        private static readonly string[] Nourriture = new string[]
        {
            "🍏", "🍎", "🍐", "🍊", "🍋", "🍌", "🍉", "🍇", "🍓", "🫐",
            "🍈", "🍒", "🍑", "🥭", "🍍", "🥥", "🥝", "🍅", "🥑", "🍆",
            "🥔", "🥕", "🌽", "🌶️", "🥒", "🥬", "🥦", "🍞", "🥐", "🥖",
            "🧀", "🍗", "🍖", "🌭", "🍔", "🍟", "🍕", "🥪", "🌮", "🌯",
            "🥗", "🍝", "🍜", "🍛", "🍣", "🍩", "🍪", "🎂", "🍰", "☕"
        };

        private static readonly string[] Objets = new string[]
        {
            "⌚", "📱", "💻", "⌨️", "🖥️", "🖨️", "🖱️", "💽", "💾", "💿",
            "📷", "📹", "🎥", "📞", "☎️", "📟", "📠", "📺", "📻", "🎙️",
            "⏰", "💣", "🔋", "🔌", "💡", "🔦", "📖", "📚", "📕", "📝",
            "✏️", "🖊️", "📎", "✂️", "🔒", "🔑", "🔨", "🛠️", "🔧", "🧲"
        };

        private static readonly string[] Symboles = new string[]
        {
            "✔️", "✖️", "❌", "⭕", "⚠️", "🚫", "💯", "❓", "❗", "💬",
            "💤", "♻️", "🆕", "🆗", "🔝", "🔥", "✨", "⭐", "🌟", "💫",
            "⚡", "☄️", "🌈", "☀️", "🌤️", "⛈️", "❄️", "🌊", "🎉", "🎊",
            "🎁", "🏆", "🎯", "🎮", "🎲", "♠️", "♥️", "♦️", "♣️", "🚀"
        };

        private static readonly string[] Drapeaux = new string[]
        {
            "🇨🇲", "🇫🇷", "🇺🇸", "🇬🇧", "🇩🇪", "🇮🇹", "🇪🇸", "🇵🇹", "🇧🇪", "🇨🇦",
            "🇧🇷", "🇳🇬", "🇬🇦", "🇨🇩", "🇬🇭", "🇨🇮", "🇸🇳", "🇲🇱", "🇹🇬", "🇧🇫",
            "🇳🇪", "🇹🇩", "🇨🇩", "🇨🇬", "🇿🇦", "🇰🇪", "🇲🇦", "🇪🇬", "🇯🇵", "🇨🇳"
        };

        /// <summary>Emojis d'une catégorie (tableau original, ne pas modifier).</summary>
        public static IReadOnlyList<string> Get(string category)
        {
            if (category == "Smileys") return Smileys;
            if (category == "Gestes") return Gestes;
            if (category == "Cœurs") return Coeurs;
            if (category == "Animaux") return Animaux;
            if (category == "Nourriture") return Nourriture;
            if (category == "Objets") return Objets;
            if (category == "Symboles") return Symboles;
            if (category == "Drapeaux") return Drapeaux;
            return Smileys;
        }
    }
}
