namespace NetLoom.Application.Security
{
    public interface ISecretProtector
    {
        byte[] Protect(byte[] plaintext);

        byte[] Unprotect(byte[] protectedData);
    }
}
