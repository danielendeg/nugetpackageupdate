# Background
In cryptography, format-preserving encryption (FPE), refers to encrypting in such a way that the output (the ciphertext) is in the same format as the input (the plaintext). The meaning of "format" varies. Typically only finite domains are discussed, for example:

* To encrypt a 16-digit credit card number so that the ciphertext is another 16-digit number.
* To encrypt an English word so that the ciphertext is another English word.
* To encrypt an n-bit number so that the ciphertext is another n-bit number (this is the definition of an n-bit block cipher).

# Ciphertext Stealing 
[Ciphertext stealing](https://en.wikipedia.org/wiki/Ciphertext_stealing) is a technique for encrypting plaintext using a block cipher, without padding the message to a multiple of the block size, so the ciphertext is **the same size** as the plaintext. \
It does this by altering processing of the last two blocks of the message. The processing of all but the last two blocks is unchanged, but a portion of the second-last block's ciphertext is "stolen" to pad the last plaintext block. The padded final block is then encrypted as usual.


![CipherText_Stealing_(CTS)_on_CBC,_encryption_mode.svg](/.attachments/CipherText_Stealing_(CTS)_on_CBC,_encryption_mode-e2df2017-b7b3-49b8-8bf3-dda0b375efc5.svg)

Even the cipher text has equal length to the plain text, there are still two issues that worth thinking over:
1. The cipher text's output space is not limited to ```[A-Za-z0-9\.\-]```, we might try the 6-bits compressed encoding to map the output character into acceptable charset.
2. As Ciphertext works on the last two blocks. It requires at least one block length in the input text. Specifically, Ciphertext stealing for CBC mode doesn't necessarily require the plaintext to be longer than one block. In the case where the plaintext is one block long or less, the Initialization vector (IV) can act as the prior block of ciphertext. In this case, the cipher text would require at least one block space, which might cause conflicts with those inputs over one block length. We should consider some workaround for plaintext length less than a block size for AES128 (128 bits / 16 bytes). 


## 6-bit compressed encoding
As we have 64 characters in total for resource Id regex, we can encode the 8-bit input string (from 1 to 64 bytes) to a same length 6-bit string, like
```
'A':000000,
'B':000001,
'C':000010,
...
'8':111100,
'9':111101,
'.':111110,
'-':111111
```
Then we can get a cipher text of the same bits. Then we can map the 6-bit character back to ```[A-Za-z0-9\.\-]```.