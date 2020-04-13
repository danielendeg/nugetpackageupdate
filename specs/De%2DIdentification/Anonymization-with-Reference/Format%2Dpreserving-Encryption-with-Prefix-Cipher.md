# Background
In cryptography, format-preserving encryption (FPE), refers to encrypting in such a way that the output (the ciphertext) is in the same format as the input (the plaintext). The meaning of "format" varies. Typically only finite domains are discussed, for example:

* To encrypt a 16-digit credit card number so that the ciphertext is another 16-digit number.
* To encrypt an English word so that the ciphertext is another English word.
* To encrypt an n-bit number so that the ciphertext is another n-bit number (this is the definition of an n-bit block cipher).

# Prefix Cipher
The prefix cipher method uses AES or 3DES algorithm. For example encrypting 16 digit credit
card number applying an AES algorithm to each digit and store the digit and encrypting values in the table. The
table is sorted according to the encrypted value and the corresponding original digits are used as a cipher text.

Thus, to create a FPE on the domain {0,1,2,3}, given a key K apply AES(K) to each integer, giving, for example,
```
weight(0) = 0x56c644080098fc5570f2b329323dbf62
weight(1) = 0x08ee98c0d05e3dad3eb3d6236f23e7b7
weight(2) = 0x47d2e1bf72264fa01fb274465e56ba20
weight(3) = 0x077de40941c93774857961a8a772650d
```

Sorting [0,1,2,3] by weight gives [3,1,2,0], so the cipher is
```
F(0) = 3
F(1) = 1
F(2) = 2
F(3) = 0
```
The technique is useful only for small range of plain text as a large lookup table is not effective.
# Analysis
![image.png](/.attachments/image-b6e01e7d-36f2-4327-9509-7693d2f2d58b.png)Under the assumption that our underlying block cipher E is ideal, I
is equally likely to be any of the permutations on M. The proof of this fact is
trivial and is omitted. The method remains good when E is secure in the sense
of a PRP. The argument is standard and is omitted.

Enciphering and deciphering are constant-time
operations. The cost here is O(k) time and space used in the initialization step.
This clearly means that this method is practical only for small values of k. A
further practical consideration is that, although this initialization is a one-time
cost, it results in a table of sensitive data which must be stored somewhere.

# Reference
1. [Wikipedia](https://en.wikipedia.org/wiki/Format-preserving_encryption#FPE_from_a_prefix_cipher)
2. [Prefix Cipher Paper](https://web.cs.ucdavis.edu/~rogaway/papers/subset.pdf)