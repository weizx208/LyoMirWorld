namespace MirCommon
{
    
    
    
    public enum SERVER_ERROR
    {
        
        
        
        SE_OK = 0,
        
        
        
        
        SE_FAIL = 1,
        
        
        
        
        SE_ALLOCMEMORYFAIL = 2,
        
        
        
        
        SE_DB_NOMOREDATA = 3,
        
        
        
        
        SE_DB_NOTINITED = 4,
        
        
        
        
        SE_LOGIN_ACCOUNTEXIST = 100,
        
        
        
        
        SE_LOGIN_ACCOUNTNOTEXIST = 101,
        
        
        
        
        SE_LOGIN_PASSWORDERROR = 102,
        
        
        
        
        SE_SELCHAR_CHAREXIST = 200,
        
        
        
        
        SE_SELCHAR_NOTEXIST = 201,
        
        
        
        
        SE_REG_INVALIDACCOUNT = 300,
        
        
        
        
        SE_REG_INVALIDPASSWORD = 301,
        
        
        
        
        SE_REG_INVALIDNAME = 302,
        
        
        
        
        SE_REG_INVALIDBIRTHDAY = 303,
        
        
        
        
        SE_REG_INVALIDPHONENUMBER = 304,
        
        
        
        
        SE_REG_INVALIDMOBILEPHONE = 305,
        
        
        
        
        SE_REG_INVALIDQUESTION = 306,
        
        
        
        
        SE_REG_INVALIDANSWER = 307,
        
        
        
        
        SE_REG_INVALIDIDCARD = 308,
        
        
        
        
        SE_REG_INVALIDEMAIL = 309,
        
        
        
        
        SE_CREATECHARACTER_INVALID_CHARNAME = 400,
        
        
        
        
        SE_ODBC_SQLCONNECTFAIL = 500,
        
        
        
        
        SE_ODBC_SQLEXECDIRECTFAIL = 501,
    }
}
