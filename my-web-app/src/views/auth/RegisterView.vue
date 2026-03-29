// ตัวอย่างการทำงานตอนกด "สมัครสมาชิก"
import { auth, db } from '@/firebase/config';
import { createUserWithEmailAndPassword } from 'firebase/auth';
import { doc, setDoc, serverTimestamp } from 'firebase/firestore';

const handleRegister = async (email, password, displayName, departmentId) => {
try {
// 1. สร้างบัญชี Firebase Auth
const userCredential = await createUserWithEmailAndPassword(auth, email, password);
const user = userCredential.user;

// 2. สร้างข้อมูล Profile ใน Firestore Collection "users"
// ค่าเริ่มต้นคือ สถานะ='pending', สิทธิ์='user', และยังเข้าใช้งานระบบไหนไม่ได้
await setDoc(doc(db, 'users', user.uid), {
email: user.email,
displayName: displayName,
departmentId: departmentId,
role: 'user',
status: 'pending',
accessibleSystems: [],
createdAt: serverTimestamp()
});

console.log("สมัครสมาชิกสำเร็จ รอแอดมินอนุมัติ!");
} catch (error) {
console.error(error);
}
};