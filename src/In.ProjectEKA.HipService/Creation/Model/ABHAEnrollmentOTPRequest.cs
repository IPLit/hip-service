using System.Collections.Generic;
using System.Linq;
using In.ProjectEKA.HipService.Common.Model;

namespace In.ProjectEKA.HipService.Creation.Model;

public class ABHAEnrollmentOTPRequest
{
    public string TxnId { get; }
    public List<string> Scope { get; }
    public string LoginHint { get; }
    public string LoginId { get; }
    public string OtpSystem { get; }
    
    public ABHAEnrollmentOTPRequest(string txnId, List<ABHAScope> scope, ABHALoginHint loginHint, string loginId, OTPSystem otpSystem)
    {
        TxnId = txnId;
        Scope = scope.Select(s => s.Value).ToList();
        LoginHint = loginHint.Value;
        LoginId = loginId;
        OtpSystem = otpSystem.Value;
    }
    
}