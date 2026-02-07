using System;

namespace Twinsanity.TwinsanityInterchange.Interfaces
{
    public interface ITwinSection : ITwinItem
    {
        /// <summary>
        /// Get length of payload
        /// </summary>
        /// <returns></returns>
        Int32 GetContentLength();
        /// <summary>
        /// Gets item by collection's id
        /// </summary>
        /// <param name="index">Item's ID</param>
        /// <returns></returns>
        ITwinItem GetItem(Int32 index);
        /// <summary>
        /// Gets item by its id
        /// </summary>
        /// <typeparam name="T">Specific item's type</typeparam>
        /// <param name="id">Item's ID</param>
        /// <returns></returns>
        T GetItem<T>(UInt32 id) where T : ITwinItem;
        /// <summary>
        /// Check if a given item exists in the section
        /// </summary>
        /// <param name="id">Item's ID</param>
        /// <returns></returns>
        bool ContainsItem(UInt32 id);
        /// <summary>
        /// Remove item by ID
        /// </summary>
        /// <typeparam name="T">Specific item's type</typeparam>
        /// <param name="id">Item's ID</param>
        void RemoveItem<T>(UInt32 id) where T : ITwinItem;
        /// <summary>
        /// Add new item
        /// </summary>
        /// <param name="item"></param>
        void AddItem(ITwinItem item);
        /// <summary>
        /// Get amount of stored items
        /// </summary>
        /// <returns></returns>
        Int32 GetItemsAmount();
        /// <summary>
        /// Removes duplicated IDs
        /// </summary>
        void RemoveDuplicates(UInt32 id);
    }
}
