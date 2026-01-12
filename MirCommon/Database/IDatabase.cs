using System.Data;

namespace MirCommon.Database
{
    
    
    
    public interface IDatabase
    {
        
        
        
        SERVER_ERROR OpenDataBase();
        
        
        
        
        SERVER_ERROR CheckAccount(string account, string password);
        
        
        
        
        SERVER_ERROR CheckAccountExist(string account);
        
        
        
        
        SERVER_ERROR CreateAccount(string account, string password, string name, string birthday,
                                   string q1, string a1, string q2, string a2, string email,
                                   string phoneNumber, string mobilePhoneNumber, string idCard);
        
        
        
        
        SERVER_ERROR ChangePassword(string account, string oldPassword, string newPassword);
        
        
        
        
        SERVER_ERROR GetCharList(string account, string serverName, out string charListData);
        
        
        
        
        SERVER_ERROR GetDelCharList(string account, string serverName, out string delCharListData);
        
        
        
        
        SERVER_ERROR CreateCharacter(string account, string serverName, string name, byte job, byte hair, byte sex);
        
        
        
        
        SERVER_ERROR CreateCharacter(string account, string serverName, string name, byte job, byte hair, byte sex, byte level);
        
        
        
        
        SERVER_ERROR CreateCharacter(CREATECHARDESC desc);
        
        
        
        
        SERVER_ERROR DelCharacter(string account, string serverName, string name);
        
        
        
        
        SERVER_ERROR RestoreCharacter(string account, string serverName, string name);
        
        
        
        
        SERVER_ERROR GetCharDBInfo(string account, string serverName, string name, out byte[] charData);
        
        
        
        
        SERVER_ERROR PutCharDBInfo(string account, string serverName, string name, byte[] charData);
        
        
        
        
        SERVER_ERROR GetMapPosition(string account, string serverName, string name, out string mapName, out short x, out short y);
        
        
        
        
        SERVER_ERROR GetFreeItemId(out uint itemId);
        
        
        
        
        SERVER_ERROR FindItemId(uint ownerId, byte flag, ushort pos, uint findKey, out uint itemId);
        
        
        
        
        SERVER_ERROR UpgradeItem(uint makeIndex, uint upgrade);
        
        
        
        
        SERVER_ERROR CreateItem(uint ownerId, byte flag, ushort pos, byte[] itemData);
        
        
        
        
        SERVER_ERROR CreateItemEx(uint ownerId, byte flag, ushort pos, byte[] itemData);
        
        
        
        
        SERVER_ERROR UpdateItem(uint ownerId, byte flag, ushort pos, byte[] itemData);
        
        
        
        
        SERVER_ERROR DeleteItem(uint itemId);
        
        
        
        
        SERVER_ERROR UpdateItemPos(uint itemId, byte flag, ushort pos);
        
        
        
        
        SERVER_ERROR UpdateItemPosEx(byte flag, ushort count, byte[] itemPosData);
        
        
        
        
        SERVER_ERROR UpdateItemOwner(uint itemId, uint ownerId, byte flag, ushort pos);
        
        
        
        
        SERVER_ERROR QueryItems(uint ownerId, byte flag, out byte[] itemsData);
        
        
        
        
        SERVER_ERROR UpdateItems(uint ownerId, byte flag, byte[] itemsData);
        
        
        
        
        SERVER_ERROR QueryMagic(uint ownerId, out byte[] magicData);
        
        
        
        
        SERVER_ERROR UpdateMagic(uint ownerId, byte[] magicData);
        
        
        
        
        SERVER_ERROR DeleteMagic(uint ownerId, ushort magicId);
        
        
        
        
        SERVER_ERROR UpdateCommunity(uint ownerId, string communityData);
        
        
        
        
        SERVER_ERROR QueryCommunity(uint ownerId, out string communityData);
        
        
        
        
        SERVER_ERROR DeleteMarriage(string name, string marriage);
        
        
        
        
        SERVER_ERROR DeleteTeacher(string name, string teacher);
        
        
        
        
        SERVER_ERROR DeleteStudent(string teacher, string student);
        
        
        
        
        SERVER_ERROR BreakFriend(string friend1, string friend2);
        
        
        
        
        SERVER_ERROR RestoreGuild(string name, string guildName);
        
        
        
        
        SERVER_ERROR AddCredit(string name, uint count);
        
        
        
        
        SERVER_ERROR QueryTaskInfo(uint ownerId, out byte[] taskInfoData);
        
        
        
        
        SERVER_ERROR UpdateTaskInfo(uint ownerId, byte[] taskInfoData);
        
        
        
        
        SERVER_ERROR QueryUpgradeItem(uint ownerId, out byte[] upgradeItemData);
        
        
        
        
        SERVER_ERROR ExecSqlCommand(string sql, out DataTable result);
        
        
        
        
        void Close();
    }
}
