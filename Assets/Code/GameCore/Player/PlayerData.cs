using System;
using UnityEngine;

namespace ReGecko.GameCore.Player
{
	[Serializable]
	public class PlayerData
	{
		public int Stamina = 100;
		public int Level = 1;
	}

	public static class PlayerService
	{
		const string StorageKey = "RG_PlayerData";
		static PlayerData _cached;



        /// <summary>
        /// 游戏状态变化事件
        /// </summary>
        public delegate void PlayerDataChangedEventHandler(object sender, EventArgs e);
        // 事件
        public static event PlayerDataChangedEventHandler OnPlayerDataChanged;


        public static PlayerData Get()
		{
			if (_cached != null) return _cached;
			if (PlayerPrefs.HasKey(StorageKey))
			{
				var json = PlayerPrefs.GetString(StorageKey);
				_cached = JsonUtility.FromJson<PlayerData>(json);
				if (_cached == null) _cached = new PlayerData();
			}
			else
			{
				_cached = new PlayerData();
				Save();
			}
			return _cached;
		}

		public static void Save()
		{
			if (_cached == null) _cached = new PlayerData();
			var json = JsonUtility.ToJson(_cached);
			PlayerPrefs.SetString(StorageKey, json);
			PlayerPrefs.Save();
		}


        public static void ChangePlayerData(PlayerData newData)
        {
			if (_cached == null) _cached = new PlayerData();

			_cached = newData;

			Save();
			// 触发事件
			OnPlayerDataChanged?.Invoke(null, new EventArgs());
        }

    }
}


